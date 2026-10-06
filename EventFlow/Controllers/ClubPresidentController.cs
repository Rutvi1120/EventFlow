using EventFlow.Data;
using EventFlow.Models;
using EventFlow.Services;
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
        private readonly VolunteerManagementService _volunteerService;
        private readonly ConflictDetectionService _conflictDetectionService;

        public ClubPresidentController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            VolunteerManagementService volunteerService,
            ConflictDetectionService conflictDetectionService)
        {
            _context = context;
            _userManager = userManager;
            _volunteerService = volunteerService;
            _conflictDetectionService = conflictDetectionService;
        }

        // =========================================================
        // DASHBOARD
        // =========================================================
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Challenge();
            }

            var userId = user.Id;

            var clubs = await _context.Clubs
                .Include(c => c.FacultySupervisor)
                .Where(c =>
                    c.ClubPresidentId == userId &&
                    c.Status == "Approved")
                .OrderBy(c => c.Name)
                .ToListAsync();

            var clubIds = clubs
                .Select(c => c.Id)
                .ToList();

            var clubEvents = await _context.Events
                .Include(e => e.Club)
                .Include(e => e.Registrations)
                .Where(e =>
                    e.ClubId.HasValue &&
                    clubIds.Contains(e.ClubId.Value))
                .OrderByDescending(e => e.StartDateTime)
                .ToListAsync();

            var totalClubEvents = clubEvents.Count;

            var upcomingEvents = clubEvents.Count(e =>
                e.ApprovalStatus == "Approved" &&
                e.StartDateTime > DateTime.Now);

            var completedEvents = clubEvents.Count(e =>
                e.EndDateTime <= DateTime.Now);

            var totalParticipants = clubEvents
                .Sum(e => e.Registrations?.Count ?? 0);

            var pendingVolunteerCount =
                await _volunteerService.CountPendingAsync(User);

            ViewBag.PresidentName = user.FullName;

            ViewBag.MyClubCount = clubs.Count;
            ViewBag.ClubEventCount = totalClubEvents;
            ViewBag.UpcomingEventCount = upcomingEvents;
            ViewBag.CompletedEventCount = completedEvents;
            ViewBag.TotalParticipants = totalParticipants;
            ViewBag.PendingVolunteerCount = pendingVolunteerCount;

            return View();
        }

        // =========================================================
        // MY CLUBS
        // =========================================================
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

        // =========================================================
        // CLUB DETAILS
        // =========================================================
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
                .Include(c => c.Events)
                    .ThenInclude(e => e.Registrations)
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    c.ClubPresidentId == userId &&
                    c.Status == "Approved");

            if (club == null)
            {
                return NotFound();
            }

            var eventIds = club.Events
                .Select(e => e.Id)
                .ToList();

            var manageableIds = await _volunteerService
                .ManageableEvents(User)
                .Where(e => eventIds.Contains(e.Id))
                .Select(e => e.Id)
                .ToListAsync();

            ViewBag.ManageableEventIds =
                new HashSet<int>(manageableIds);

            return View(club);
        }

        // =========================================================
        // CREATE EVENT - GET
        // =========================================================
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

            var eventItem = new Event
            {
                EventType = "Club",
                ClubId = club.Id,
                ClubName = club.Name,
                ApprovalStatus = "Pending",
                Status = "Upcoming"
            };

            return View(eventItem);
        }

        // =========================================================
        // CREATE EVENT - POST
        // =========================================================
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

            await EventRules.ValidateVenueAsync(
                _context,
                ModelState,
                eventItem);

            if (eventItem.VenueId > 0)
            {
                var venueConflict =
                    await _conflictDetectionService.FindVenueConflictAsync(
                        eventItem.VenueId,
                        eventItem.StartDateTime,
                        eventItem.EndDateTime);

                if (venueConflict != null)
                {
                    ModelState.AddModelError(
                        nameof(Event.VenueId),
                        $"Venue conflict: {venueConflict.Name} is already scheduled " +
                        $"from {venueConflict.StartDateTime:g} to " +
                        $"{venueConflict.EndDateTime:g}.");
                }
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

        // =========================================================
        // CLUB EVENTS
        // =========================================================
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
                .Include(e => e.Registrations)
                .Where(e => e.OrganizerId == userId)
                .OrderByDescending(e => e.StartDateTime)
                .ToListAsync();

            var eventIds = events
                .Select(e => e.Id)
                .ToList();

            var manageableIds = await _volunteerService
                .ManageableEvents(User)
                .Where(e => eventIds.Contains(e.Id))
                .Select(e => e.Id)
                .ToListAsync();

            ViewBag.ManageableEventIds =
                new HashSet<int>(manageableIds);

            return View(events);
        }

        // =========================================================
        // EDIT EVENT - GET
        // =========================================================
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

        // =========================================================
        // EDIT EVENT - POST
        // =========================================================
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

            await EventRules.ValidateVenueAsync(
                _context,
                ModelState,
                eventItem);

            if (eventItem.VenueId > 0)
            {
                var venueConflict =
                    await _conflictDetectionService.FindVenueConflictAsync(
                        eventItem.VenueId,
                        eventItem.StartDateTime,
                        eventItem.EndDateTime,
                        id);

                if (venueConflict != null)
                {
                    ModelState.AddModelError(
                        nameof(Event.VenueId),
                        $"Venue conflict: {venueConflict.Name} is already scheduled " +
                        $"from {venueConflict.StartDateTime:g} to " +
                        $"{venueConflict.EndDateTime:g}.");
                }
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

        // =========================================================
        // DELETE EVENT
        // =========================================================
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

        // =========================================================
        // VENUES
        // =========================================================
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