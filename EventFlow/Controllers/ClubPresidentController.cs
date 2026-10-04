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
            var myClubCount = await _context.Clubs
    .CountAsync(c => c.ClubPresidentId == userId);

            var clubEventCount = await _context.Events
                .CountAsync(e => e.Club != null &&
                                 e.Club.ClubPresidentId == userId);

            var pendingVolunteerCount =
    await _volunteerService.CountPendingAsync(User);

            ViewBag.MyClubCount = myClubCount;
            ViewBag.ClubEventCount = clubEventCount;
            ViewBag.PendingVolunteerCount = pendingVolunteerCount;
            ViewBag.Clubs = clubs;
            ViewBag.Events = events;
            ViewBag.PendingVolunteerCount =
                await _volunteerService.CountPendingAsync(User);

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

            await EventRules.ValidateVenueAsync(
    _context,
    ModelState,
    eventItem);

            // =====================================================
            // CHECK VENUE TIME CONFLICT
            // =====================================================

            if (eventItem.VenueId>0)
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

            var eventIds = events.Select(e => e.Id).ToList();

            var manageableIds = await _volunteerService.ManageableEvents(User)
                .Where(e => eventIds.Contains(e.Id))
                .Select(e => e.Id)
                .ToListAsync();

            ViewBag.ManageableEventIds = new HashSet<int>(manageableIds);

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

            await EventRules.ValidateVenueAsync(
    _context,
    ModelState,
    eventItem);

            // =====================================================
            // CHECK VENUE TIME CONFLICT
            // Exclude the event currently being edited.
            // =====================================================

            if (eventItem.VenueId>0)
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

        // =====================================================
        // VOLUNTEER MANAGEMENT - DIRECT ADD
        // Same workflow as Faculty/AddVolunteer, limited to the
        // president's own club events (enforced by the service).
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> AddVolunteer(int eventId)
        {
            var (access, eventItem) =
                await _volunteerService.CheckDirectAddAccessAsync(eventId, User);

            var blocked = DirectAddRedirect(access, eventId);

            if (blocked != null)
            {
                return blocked;
            }

            return View(
                "~/Views/Faculty/AddVolunteer.cshtml",
                await BuildAddVolunteerModelAsync(
                    eventItem!,
                    new AddVolunteerViewModel { EventId = eventId }));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddVolunteer(AddVolunteerViewModel model)
        {
            var (access, eventItem) =
                await _volunteerService.CheckDirectAddAccessAsync(model.EventId, User);

            var blocked = DirectAddRedirect(access, model.EventId);

            if (blocked != null)
            {
                return blocked;
            }

            if (ModelState.IsValid)
            {
                var result = await _volunteerService
                    .AddVolunteerDirectlyAsync(eventItem!, model);

                switch (result)
                {
                    case DirectAddResult.Success:
                        TempData["SuccessMessage"] =
                            "Volunteer added successfully. The student can see the assignment now.";

                        return RedirectToAction(
                            "EventVolunteers",
                            "Volunteer",
                            new { eventId = model.EventId });

                    case DirectAddResult.Duplicate:
                        ModelState.AddModelError(
                            nameof(model.VolunteerId),
                            "This student is already a volunteer (or has applied) for this event.");
                        break;

                    default:
                        ModelState.AddModelError(
                            nameof(model.VolunteerId),
                            "Please select a valid student.");
                        break;
                }
            }

            return View(
                "~/Views/Faculty/AddVolunteer.cshtml",
                await BuildAddVolunteerModelAsync(eventItem!, model));
        }

        private IActionResult? DirectAddRedirect(DirectAddAccess access, int eventId)
        {
            switch (access)
            {
                case DirectAddAccess.EventNotFound:
                    return NotFound();

                case DirectAddAccess.NotAuthorized:
                    TempData["ErrorMessage"] =
                        "You are not authorized to manage volunteers for this event.";

                    return RedirectToAction("Manage", "Volunteer");

                case DirectAddAccess.EventUnavailable:
                    TempData["ErrorMessage"] =
                        "Volunteers can only be added to approved events that have not ended.";

                    return RedirectToAction(
                        "EventVolunteers",
                        "Volunteer",
                        new { eventId });

                default:
                    return null;
            }
        }

        private async Task<AddVolunteerViewModel> BuildAddVolunteerModelAsync(
            Event eventItem,
            AddVolunteerViewModel model)
        {
            model.EventId = eventItem.Id;
            model.Event = eventItem;
            model.FormController = "ClubPresident";
            model.Students =
                await _volunteerService.GetStudentOptionsAsync(eventItem.Id);

            return model;
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