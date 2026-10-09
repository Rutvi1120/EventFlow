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
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        private readonly EventBannerService _eventBannerService;
        public StudentController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    EventBannerService eventBannerService)
{
    _context = context;
    _userManager = userManager;
    _eventBannerService = eventBannerService;
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

            var myEventsCount = await _context.Events
                .CountAsync(e => e.OrganizerId == userId);

            var volunteerAssignments = await _context.Volunteers
                .Include(v => v.Event)
                .ThenInclude(e => e!.Venue)
                .Where(v => v.VolunteerId == userId)
                .OrderByDescending(v => v.Event!.StartDateTime)
                .ToListAsync();

            ViewBag.UpcomingEvents = upcomingEvents;
            ViewBag.Registrations = registrations;
            ViewBag.VolunteerAssignments = volunteerAssignments;
            ViewBag.MyEventsCount = myEventsCount;
            ViewBag.RegistrationsCount = registrations.Count;
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
            await LoadFacultySupervisors();

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
        public async Task<IActionResult> CreateEvent(Event eventItem, IFormFile? bannerImage)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            eventItem.OrganizerId = userId;

            eventItem.EventType = EventTypes.Student;
            eventItem.ApprovalStatus = EventApprovalStatus.Pending;
            eventItem.Status = "Upcoming";
            eventItem.ClubId = null;
            eventItem.ClubName = null;

            if (string.IsNullOrWhiteSpace(eventItem.FacultySupervisorId))
            {
                ModelState.AddModelError(
                    nameof(Event.FacultySupervisorId),
                    "Please select a Faculty Supervisor.");
            }
            else
            {
                var faculty =
                    await _userManager.FindByIdAsync(
                        eventItem.FacultySupervisorId);

                if (faculty == null ||
                    !await _userManager.IsInRoleAsync(
                        faculty,
                        AppRoles.Faculty))
                {
                    ModelState.AddModelError(
                        nameof(Event.FacultySupervisorId),
                        "Please select a valid Faculty Supervisor.");
                }
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

            if (!ModelState.IsValid)
            {
                await LoadVenues();
                await LoadFacultySupervisors();

                return View(eventItem);
            }
            try
            {
                eventItem.BannerImagePath =
                    await _eventBannerService.SaveBannerAsync(bannerImage);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(
                    "bannerImage",
                    ex.Message);

                await LoadVenues();
                await LoadFacultySupervisors();

                return View(eventItem);
            }
            _context.Events.Add(eventItem);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Event submitted successfully. The selected Faculty Supervisor will review it.";

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
            Event eventItem, IFormFile? bannerImage)
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
            if (bannerImage != null && bannerImage.Length > 0)
            {
                try
                {
                    var oldBanner = existingEvent.BannerImagePath;

                    existingEvent.BannerImagePath =
                        await _eventBannerService.SaveBannerAsync(
                            bannerImage);

                    _eventBannerService.DeleteBanner(oldBanner);
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError(
                        "bannerImage",
                        ex.Message);

                    return View(eventItem);
                }
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

            await EventRules.ValidateVenueAsync(_context, ModelState, eventItem);

            if (!ModelState.IsValid)
            {
                await LoadVenues();
                await LoadFacultySupervisors();

                return View(eventItem);
            }
            if (string.IsNullOrWhiteSpace(eventItem.FacultySupervisorId))
            {
                ModelState.AddModelError(
                    nameof(Event.FacultySupervisorId),
                    "Please select a Faculty Supervisor.");
            }
            else
            {
                var faculty =
                    await _userManager.FindByIdAsync(
                        eventItem.FacultySupervisorId);

                if (faculty == null ||
                    !await _userManager.IsInRoleAsync(
                        faculty,
                        AppRoles.Faculty))
                {
                    ModelState.AddModelError(
                        nameof(Event.FacultySupervisorId),
                        "Please select a valid Faculty Supervisor.");
                }
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

            var bannerPath = eventItem.BannerImagePath;

            _context.Events.Remove(eventItem);

            await _context.SaveChangesAsync();

            _eventBannerService.DeleteBanner(bannerPath);

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

        private async Task LoadFacultySupervisors()
        {
            var facultyUsers =
                await _userManager.GetUsersInRoleAsync(
                    AppRoles.Faculty);

            ViewBag.FacultySupervisors = facultyUsers
                .OrderBy(f => f.FullName)
                .ToList();
        }
    }
    }