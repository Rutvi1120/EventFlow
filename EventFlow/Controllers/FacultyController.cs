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
    [Authorize(Roles = "Faculty")]
    public class FacultyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly VolunteerManagementService _volunteerService;

        private readonly EventBannerService _eventBannerService;
        public FacultyController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            VolunteerManagementService volunteerService,
            EventBannerService eventBannerService)
        {
            _context = context;
            _userManager = userManager;
            _volunteerService = volunteerService;
            _eventBannerService = eventBannerService;
        }

        public async Task<IActionResult> Index()
        {
            var facultyId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(facultyId))
                return Challenge();

            var supervisedClubs = await _context.Clubs
                .Include(c => c.ClubPresident)
                .Include(c => c.FacultySupervisor)
                .Where(c => c.FacultySupervisorId == facultyId)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var pendingEvents = await _context.Events
                .Include(e => e.Organizer)
                .Include(e => e.Club)
                .Include(e => e.Venue)
                .Where(e =>
                    e.ApprovalStatus == EventApprovalStatus.Pending &&
                    (
                        (e.EventType == EventTypes.Student &&
                         e.FacultySupervisorId == facultyId) ||
                        e.EventType == EventTypes.College ||
                        (
                            e.EventType == EventTypes.Club &&
                            e.Club != null &&
                            e.Club.FacultySupervisorId == facultyId
                        )
                    ))
                .OrderBy(e => e.StartDateTime)
                .ToListAsync();

            var myEvents = await _context.Events
                .Include(e => e.Club)
                .Include(e => e.Venue)
                .Where(e => e.OrganizerId == facultyId)
                .OrderByDescending(e => e.StartDateTime)
                .ToListAsync();



            var pendingVolunteerApplications =
                await _volunteerService.ManageableVolunteers(User)
                    .Include(v => v.VolunteerUser)
                    .Include(v => v.Event)
                    .ThenInclude(e => e!.Club)
                    .Where(v => v.Status == VolunteerStatus.Pending)
                    .OrderBy(v => v.Event!.StartDateTime)
                    .ThenBy(v => v.VolunteerUser!.FullName)
                    .ToListAsync();

            var pendingPresidentRequests = await _userManager.Users
                .Include(u => u.RequestedClub)
                .Where(u =>
                    u.RequestedRole == "ClubPresident" &&
                    u.IsApproved &&
                    u.RequestedClubId != null &&
                    u.RequestedClub != null &&
                    u.RequestedClub.FacultySupervisorId == facultyId &&
                    u.RequestedClub.Status == "Approved")
                .OrderBy(u => u.FullName)
                .ToListAsync();

            ViewBag.SupervisedClubs = supervisedClubs;
            ViewBag.PendingEvents = pendingEvents;
            ViewBag.MyEvents = myEvents;
            ViewBag.PendingPresidentRequests = pendingPresidentRequests;


            ViewBag.PendingVolunteerApplications =
                pendingVolunteerApplications;

            ViewBag.PendingVolunteerCount =
                pendingVolunteerApplications.Count;

            return View();
        }


        public async Task<IActionResult> Events()
        {
            var facultyId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(facultyId))
                return Challenge();

            var events = await _context.Events
                .Include(e => e.Organizer)
                .Include(e => e.Club)
                .Include(e => e.Venue)
                .Where(e =>
                    e.OrganizerId == facultyId ||


                    (
                        e.EventType == EventTypes.Student &&
                        e.FacultySupervisorId == facultyId
                    ) ||


                    (
                        e.ApprovalStatus == EventApprovalStatus.Pending &&
                        e.EventType == EventTypes.College
                    ) ||


                    (
                        e.EventType == EventTypes.Club &&
                        e.Club != null &&
                        e.Club.FacultySupervisorId == facultyId
                    ))
                .OrderByDescending(e => e.StartDateTime)
                .ToListAsync();

            var eventIds = events.Select(e => e.Id).ToList();

            var manageableIds = await _volunteerService.ManageableEvents(User)
                .Where(e => eventIds.Contains(e.Id))
                .Select(e => e.Id)
                .ToListAsync();

            ViewBag.ManageableEventIds = new HashSet<int>(manageableIds);
            ViewBag.FacultyId = facultyId;

            return View(events);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveEvent(int id)
        {
            var facultyId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(facultyId))
                return Challenge();

            var eventItem = await _context.Events
                .Include(e => e.Club)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (eventItem == null)
                return NotFound();

            if (eventItem.ApprovalStatus != "Pending")
                return BadRequest();

            var canApprove = eventItem.EventType switch
            {
                "Student" =>
                    eventItem.FacultySupervisorId == facultyId,

                "College" =>
                    true,

                "Club" =>
                    eventItem.Club != null &&
                    eventItem.Club.FacultySupervisorId == facultyId,

                _ => false
            };
            if (!canApprove)
                return Forbid();

            eventItem.ApprovalStatus = "Approved";
            eventItem.Status = "Upcoming";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Event approved successfully.";

            return RedirectToAction(nameof(Events));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectEvent(int id)
        {
            var facultyId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(facultyId))
                return Challenge();

            var eventItem = await _context.Events
                .Include(e => e.Club)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (eventItem == null)
                return NotFound();

            if (eventItem.ApprovalStatus != "Pending")
                return BadRequest();

            var canReject = eventItem.EventType switch
            {
                "Student" =>
                    eventItem.FacultySupervisorId == facultyId,

                "College" =>
                    true,

                "Club" =>
                    eventItem.Club != null &&
                    eventItem.Club.FacultySupervisorId == facultyId,

                _ => false
            };

            if (!canReject)
                return Forbid();

            eventItem.ApprovalStatus = "Rejected";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Event rejected successfully.";

            return RedirectToAction(nameof(Events));
        }



        public async Task<IActionResult> AllClubs()
        {
            var facultyId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(facultyId))
                return Challenge();

            var clubs = await _context.Clubs
                .Include(c => c.ClubPresident)
                .Include(c => c.FacultySupervisor)
                .Where(c => c.FacultySupervisorId == facultyId)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(clubs);
        }

        public async Task<IActionResult> ClubDetails(int id)
        {
            var facultyId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(facultyId))
                return Challenge();

            var club = await _context.Clubs
                .Include(c => c.ClubPresident)
                .Include(c => c.FacultySupervisor)
                .Include(c => c.Events)
                .FirstOrDefaultAsync(c =>
                    c.Id == id &&
                    c.FacultySupervisorId == facultyId);

            if (club == null)
                return NotFound();

            var presidentRequests = await _userManager.Users
                .Where(u =>
                    u.RequestedRole == "ClubPresident" &&
                    u.IsApproved &&
                    u.RequestedClubId == club.Id)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            ViewBag.PresidentRequests = presidentRequests;

            return View(club);
        }

        [HttpGet]
        public IActionResult CreateClub()
        {
            return View(new Club());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClub(Club model)
        {
            var facultyId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(facultyId))
                return Unauthorized();

            ModelState.Remove(nameof(Club.FacultySupervisorId));
            ModelState.Remove(nameof(Club.ClubPresidentId));

            model.FacultySupervisorId = facultyId;
            model.ClubPresidentId = null;
            model.Status = "Pending";

            if (!ModelState.IsValid)
                return View(model);

            _context.Clubs.Add(model);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Club created and submitted for approval.";

            return RedirectToAction(nameof(AllClubs));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePresident(string userId)
        {
            var facultyId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(facultyId))
                return Challenge();

            var user = await _userManager.Users
                .FirstOrDefaultAsync(u =>
                    u.Id == userId &&
                    u.RequestedRole == "ClubPresident" &&
                    u.IsApproved);

            if (user == null || user.RequestedClubId == null)
                return NotFound();

            var club = await _context.Clubs
                .FirstOrDefaultAsync(c =>
                    c.Id == user.RequestedClubId.Value &&
                    c.FacultySupervisorId == facultyId &&
                    c.Status == "Approved");

            if (club == null)
                return Forbid();

            if (!string.IsNullOrWhiteSpace(club.ClubPresidentId))
            {
                TempData["ErrorMessage"] =
                    "This club already has a club president.";

                return RedirectToAction(
                    nameof(ClubDetails),
                    new { id = club.Id });
            }

            club.ClubPresidentId = user.Id;
            user.RequestedClubId = null;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"{user.FullName} has been assigned as club president.";

            return RedirectToAction(
                nameof(ClubDetails),
                new { id = club.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectPresident(string userId)
        {
            var facultyId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(facultyId))
                return Challenge();

            var user = await _userManager.Users
                .Include(u => u.RequestedClub)
                .FirstOrDefaultAsync(u =>
                    u.Id == userId &&
                    u.RequestedRole == "ClubPresident" &&
                    u.IsApproved);

            if (user == null || user.RequestedClubId == null)
                return NotFound();

            var club = await _context.Clubs
                .FirstOrDefaultAsync(c =>
                    c.Id == user.RequestedClubId.Value &&
                    c.FacultySupervisorId == facultyId &&
                    c.Status == "Approved");

            if (club == null)
                return Forbid();

            user.RequestedClubId = null;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"{user.FullName}'s club president request was rejected.";

            return RedirectToAction(
                nameof(ClubDetails),
                new { id = club.Id });
        }



        [HttpGet]
        public async Task<IActionResult> CreateEvent()
        {
            await LoadVenues();

            return View(new Event
            {
                EventType = "College",
                ApprovalStatus = "Approved",
                Status = "Upcoming"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEvent(Event eventItem, IFormFile? bannerImage)
        {
            var facultyId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(facultyId))
                return Challenge();

            eventItem.OrganizerId = facultyId;
            eventItem.EventType = "College";
            eventItem.ApprovalStatus = "Approved";
            eventItem.Status = "Upcoming";
            eventItem.ClubId = null;
            eventItem.ClubName = null;

            if (eventItem.EndDateTime <= eventItem.StartDateTime)
            {
                ModelState.AddModelError(
                    nameof(Event.EndDateTime),
                    "End time must be later than start time.");
            }

            var venue = await _context.Venues
                .FirstOrDefaultAsync(v =>
                    v.Id == eventItem.VenueId);

            if (venue == null)
            {
                ModelState.AddModelError(
                    nameof(Event.VenueId),
                    "The selected venue does not exist.");
            }
            else if (eventItem.MaxParticipants > venue.Capacity)
            {
                ModelState.AddModelError(
                    nameof(Event.MaxParticipants),
                    $"This venue can accommodate only {venue.Capacity} participants.");
            }

            if (!ModelState.IsValid)
            {
                await LoadVenues();
                return View(eventItem);
            }
            try
            {
                eventItem.BannerImagePath =
                    await _eventBannerService.SaveBannerAsync(
                        bannerImage);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(
                    "bannerImage",
                    ex.Message);

                await LoadVenues();
                return View(eventItem);
            }

            _context.Events.Add(eventItem);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "College event created successfully.";

            return RedirectToAction(nameof(Events));
        }


        [HttpGet]
        public async Task<IActionResult> EditEvent(int id)
        {
            var facultyId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(facultyId))
                return Challenge();

            // Faculty may edit only college events that they created.
            var eventItem = await _context.Events.FirstOrDefaultAsync(e =>
                e.Id == id &&
                e.OrganizerId == facultyId &&
                e.EventType == "College");

            if (eventItem == null)
                return NotFound();

            await LoadVenues();
            return View(eventItem);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEvent(int id, Event model, IFormFile? bannerImage)
        {
            var facultyId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(facultyId))
                return Challenge();

            var existingEvent = await _context.Events.FirstOrDefaultAsync(e =>
                e.Id == id &&
                e.OrganizerId == facultyId &&
                e.EventType == "College");

            if (existingEvent == null)
                return NotFound();

            if (model.EndDateTime <= model.StartDateTime)
                ModelState.AddModelError(nameof(Event.EndDateTime), "End time must be later than start time.");

            var venue = await _context.Venues.FirstOrDefaultAsync(v => v.Id == model.VenueId);
            if (venue == null)
                ModelState.AddModelError(nameof(Event.VenueId), "The selected venue does not exist.");
            else if (model.MaxParticipants > venue.Capacity)
                ModelState.AddModelError(nameof(Event.MaxParticipants), $"This venue can accommodate only {venue.Capacity} participants.");

            if (!ModelState.IsValid)
            {
                model.Id = id;
                model.BannerImagePath = existingEvent.BannerImagePath;
                await LoadVenues();
                return View(model);
            }

            string? newBannerPath = null;
            if (bannerImage != null && bannerImage.Length > 0)
            {
                try
                {
                    newBannerPath = await _eventBannerService.SaveBannerAsync(bannerImage);
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError("bannerImage", ex.Message);
                    model.Id = id;
                    model.BannerImagePath = existingEvent.BannerImagePath;
                    await LoadVenues();
                    return View(model);
                }
            }

            existingEvent.Name = model.Name;
            existingEvent.Description = model.Description;
            existingEvent.StartDateTime = model.StartDateTime;
            existingEvent.EndDateTime = model.EndDateTime;
            existingEvent.MaxParticipants = model.MaxParticipants;
            existingEvent.VenueId = model.VenueId;
            existingEvent.Status = "Upcoming";
            // Keep the event's current approval state; editing does not silently change it.

            if (!string.IsNullOrWhiteSpace(newBannerPath))
            {
                var oldBannerPath = existingEvent.BannerImagePath;
                existingEvent.BannerImagePath = newBannerPath;
                await _context.SaveChangesAsync();
                _eventBannerService.DeleteBanner(oldBannerPath);
            }
            else
            {
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "College event updated successfully.";
            return RedirectToAction(nameof(Events));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var facultyId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(facultyId))
                return Challenge();

            // Never allow deletion of another organizer's or a supervised event.
            var eventItem = await _context.Events
                .Include(e => e.Registrations)
                .Include(e => e.WaitlistEntries)
                .Include(e => e.Volunteers)
                .FirstOrDefaultAsync(e =>
                    e.Id == id &&
                    e.OrganizerId == facultyId &&
                    e.EventType == "College");

            if (eventItem == null)
                return NotFound();

            // Preserve attendee/volunteer records and avoid foreign-key failures.
            if (eventItem.Registrations.Any() || eventItem.WaitlistEntries.Any() || eventItem.Volunteers.Any())
            {
                TempData["ErrorMessage"] = "This event cannot be deleted because it already has registrations, waitlist entries, or volunteers.";
                return RedirectToAction(nameof(Events));
            }

            var bannerPath = eventItem.BannerImagePath;
            _context.Events.Remove(eventItem);
            await _context.SaveChangesAsync();
            _eventBannerService.DeleteBanner(bannerPath);

            TempData["SuccessMessage"] = "College event deleted successfully.";
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