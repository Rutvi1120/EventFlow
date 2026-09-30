using EventFlow.Data;
using EventFlow.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public StudentController(
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

            var upcomingEvents = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.Organizer)
                .Include(e => e.Club)
                .Where(e =>
                    e.ApprovalStatus == "Approved" &&
                    e.EndDateTime >= DateTime.Now)
                .OrderBy(e => e.StartDateTime)
                .ToListAsync();

            var registrations = await _context.Registrations
                .Include(r => r.Event)
                .ThenInclude(e => e!.Venue)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RegisteredAt)
                .ToListAsync();

            var volunteerAssignments = await _context.Volunteers
                .Include(v => v.Event)
                .ThenInclude(e => e!.Venue)
                .Where(v => v.VolunteerId == userId)
                .OrderByDescending(v => v.Event!.StartDateTime)
                .ToListAsync();

            ViewBag.UpcomingEvents = upcomingEvents;
            ViewBag.Registrations = registrations;
            ViewBag.VolunteerAssignments = volunteerAssignments;

            return View();
        }

        public async Task<IActionResult> Events()
        {
            var events = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.Organizer)
                .Include(e => e.Club)
                .Where(e =>
                    e.ApprovalStatus == "Approved" &&
                    e.EndDateTime >= DateTime.Now)
                .OrderBy(e => e.StartDateTime)
                .ToListAsync();

            return View(events);
        }

        [HttpGet]
        public async Task<IActionResult> CreateEvent()
        {
            await LoadVenues();

            var eventItem = new Event
            {
                EventType = "Student",
                ApprovalStatus = "Pending",
                Status = "Upcoming"
            };

            return View(eventItem);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEvent(Event eventItem)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            eventItem.OrganizerId = userId;
            eventItem.EventType = "Student";
            eventItem.ApprovalStatus = "Pending";
            eventItem.Status = "Upcoming";
            eventItem.ClubId = null;
            eventItem.ClubName = null;

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
                    "Please select a valid venue.");
            }

            if (!ModelState.IsValid)
            {
                await LoadVenues();
                return View(eventItem);
            }

            _context.Events.Add(eventItem);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(MyEvents));
        }

        public async Task<IActionResult> MyEvents()
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
                .FirstOrDefaultAsync(e =>
                    e.Id == id &&
                    e.OrganizerId == userId &&
                    e.EventType == "Student");

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
                .FirstOrDefaultAsync(e =>
                    e.Id == id &&
                    e.OrganizerId == userId &&
                    e.EventType == "Student");

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
                    "Please select a valid venue.");
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
            existingEvent.EventType = "Student";
            existingEvent.ClubId = null;
            existingEvent.ClubName = null;
            existingEvent.Status = "Upcoming";
            existingEvent.ApprovalStatus = "Pending";

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(MyEvents));
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
                    e.EventType == "Student");

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

            return RedirectToAction(nameof(MyEvents));
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