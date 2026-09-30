using EventFlow.Data;
using EventFlow.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Controllers
{
    [Authorize(Roles = "ClubPresident")]
    public class ClubPresidentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ClubPresidentController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var clubs = await _context.Clubs
                .Include(c => c.FacultySupervisor)
                .Where(c =>
                    c.ClubPresidentId == userId &&
                    c.Status == "Approved")
                .OrderBy(c => c.Name)
                .ToListAsync();

            var events = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.Club)
                .Where(e => e.OrganizerId == userId)
                .OrderByDescending(e => e.StartDateTime)
                .ToListAsync();

            ViewBag.Clubs = clubs;
            ViewBag.Events = events;

            return View();
        }

        public async Task<IActionResult> AllClubs()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var clubs = await _context.Clubs
                .Include(c => c.FacultySupervisor)
                .Include(c => c.Events)
                .Where(c =>
                    c.ClubPresidentId == userId &&
                    c.Status == "Approved")
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(clubs);
        }

        public async Task<IActionResult> ClubDetails(int id)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var club = await _context.Clubs
                .Include(c => c.ClubPresident)
                .Include(c => c.FacultySupervisor)
                .Include(c => c.Events)
                .ThenInclude(e => e.Venue)
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    c.ClubPresidentId == userId &&
                    c.Status == "Approved");

            if (club == null)
            {
                return NotFound();
            }

            return View(club);
        }

        [HttpGet]
        public async Task<IActionResult> CreateEvent(int clubId)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var club = await _context.Clubs
                .FirstOrDefaultAsync(c =>
                    c.Id == clubId &&
                    c.ClubPresidentId == userId &&
                    c.Status == "Approved");

            if (club == null)
            {
                return NotFound();
            }

            await LoadVenues();

            ViewBag.Club = club;

            return View(new Event
            {
                EventType = "Club",
                ClubId = club.Id,
                ClubName = club.Name,
                ApprovalStatus = "Pending",
                Status = "Upcoming"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEvent(
            int clubId,
            Event eventItem)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var club = await _context.Clubs
                .FirstOrDefaultAsync(c =>
                    c.Id == clubId &&
                    c.ClubPresidentId == userId &&
                    c.Status == "Approved");

            if (club == null)
            {
                return NotFound();
            }

            eventItem.OrganizerId = userId;
            eventItem.EventType = "Club";
            eventItem.ClubId = club.Id;
            eventItem.ClubName = club.Name;
            eventItem.ApprovalStatus = "Pending";
            eventItem.Status = "Upcoming";

            if (eventItem.EndDateTime <= eventItem.StartDateTime)
            {
                ModelState.AddModelError(
                    nameof(Event.EndDateTime),
                    "End time must be later than start time.");
            }

            var venueExists = await _context.Venues
                .AnyAsync(v => v.Id == eventItem.VenueId);

            if (!venueExists)
            {
                ModelState.AddModelError(
                    nameof(Event.VenueId),
                    "The selected venue does not exist.");
            }

            if (!ModelState.IsValid)
            {
                await LoadVenues();
                ViewBag.Club = club;

                return View(eventItem);
            }

            _context.Events.Add(eventItem);

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(ClubDetails),
                new { id = club.Id });
        }

        public async Task<IActionResult> Events()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var events = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.Club)
                .Where(e => e.OrganizerId == userId)
                .OrderByDescending(e => e.StartDateTime)
                .ToListAsync();

            return View(events);
        }

        [HttpGet]
        public async Task<IActionResult> EditEvent(int id)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var eventItem = await _context.Events
                .Include(e => e.Club)
                .FirstOrDefaultAsync(e =>
                    e.Id == id &&
                    e.OrganizerId == userId &&
                    e.EventType == "Club");

            if (eventItem == null)
            {
                return NotFound();
            }

            if (eventItem.ApprovalStatus == "Approved")
            {
                return BadRequest();
            }

            await LoadVenues();

            return View(eventItem);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEvent(
            int id,
            Event eventItem)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var existingEvent = await _context.Events
                .Include(e => e.Club)
                .FirstOrDefaultAsync(e =>
                    e.Id == id &&
                    e.OrganizerId == userId &&
                    e.EventType == "Club");

            if (existingEvent == null)
            {
                return NotFound();
            }

            if (existingEvent.ApprovalStatus == "Approved")
            {
                return BadRequest();
            }

            if (eventItem.EndDateTime <= eventItem.StartDateTime)
            {
                ModelState.AddModelError(
                    nameof(Event.EndDateTime),
                    "End time must be later than start time.");
            }

            var venueExists = await _context.Venues
                .AnyAsync(v => v.Id == eventItem.VenueId);

            if (!venueExists)
            {
                ModelState.AddModelError(
                    nameof(Event.VenueId),
                    "The selected venue does not exist.");
            }

            if (!ModelState.IsValid)
            {
                await LoadVenues();
                return View(eventItem);
            }

            existingEvent.Name = eventItem.Name;
            existingEvent.Description = eventItem.Description;
            existingEvent.StartDateTime = eventItem.StartDateTime;
            existingEvent.EndDateTime = eventItem.EndDateTime;
            existingEvent.MaxParticipants = eventItem.MaxParticipants;
            existingEvent.VenueId = eventItem.VenueId;
            existingEvent.EventType = "Club";
            existingEvent.ClubName = existingEvent.Club?.Name;
            existingEvent.ApprovalStatus = "Pending";
            existingEvent.Status = "Upcoming";

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Events));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e =>
                    e.Id == id &&
                    e.OrganizerId == userId &&
                    e.EventType == "Club");

            if (eventItem == null)
            {
                return NotFound();
            }

            if (eventItem.ApprovalStatus == "Approved")
            {
                return BadRequest();
            }

            _context.Events.Remove(eventItem);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Events));
        }

        private async Task LoadVenues()
        {
            var venues = await _context.Venues
                .OrderBy(v => v.Name)
                .ToListAsync();

            ViewBag.Venues = new SelectList(
                venues,
                "Id",
                "Name");
        }
    }
}