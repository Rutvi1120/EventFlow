using EventFlow.Data;
using EventFlow.Models;
using EventFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Controllers
{
    [Authorize]
    public class VolunteerController : Controller
    {
        private const string NotAuthorizedMessage =
            "You are not authorized to manage volunteers for this event.";

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly VolunteerManagementService _volunteerService;
        private readonly ConflictDetectionService _conflictDetectionService;
        public VolunteerController(
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

       
        [Authorize(Roles = AppRoles.Student)]
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var now = DateTime.Now;

            var events = await _context.Events
                .AsNoTracking()
                .Include(e => e.Venue)
                .Include(e => e.Club)
                .Where(e =>
                    e.ApprovalStatus == EventApprovalStatus.Approved &&
                    e.EndDateTime > now)
                .OrderBy(e => e.StartDateTime)
                .ToListAsync();

            ViewBag.MyApplications = await _context.Volunteers
                .AsNoTracking()
                .Where(v => v.VolunteerId == userId)
                .ToDictionaryAsync(v => v.EventId, v => v.Status);

            return View(events);
        }

        
        [HttpGet]
        [Authorize(Roles = AppRoles.Student)]
        public async Task<IActionResult> Apply(int eventId)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var eventItem = await LoadEventAsync(eventId);

            var blocked = await CheckApplyEligibilityAsync(eventItem, userId);

            if (blocked != null)
            {
                return blocked;
            }

            return View(new VolunteerApplyViewModel
            {
                EventId = eventId,
                Event = eventItem
            });
        }

       

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.Student)]
        public async Task<IActionResult> Apply(VolunteerApplyViewModel model)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var eventItem = await LoadEventAsync(model.EventId);

            var blocked = await CheckApplyEligibilityAsync(eventItem, userId);

            if (blocked != null)
            {
                return blocked;
            }

            if (!ModelState.IsValid)
            {
                model.Event = eventItem;

                return View(model);
            }

            var volunteer = new Volunteer
            {
                EventId = eventItem!.Id,
                VolunteerId = userId,
                Role = string.Empty,
                Notes = string.IsNullOrWhiteSpace(model.Notes)
                    ? null
                    : model.Notes.Trim(),
                Status = VolunteerStatus.Pending,
                AssignedAt = null,
                CompletedAt = null
            };

            _context.Volunteers.Add(volunteer);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                
                _context.Entry(volunteer).State = EntityState.Detached;

                var duplicate = await _context.Volunteers.AnyAsync(v =>
                    v.EventId == eventItem.Id &&
                    v.VolunteerId == userId);

                if (duplicate)
                {
                    TempData["InfoMessage"] =
                        "You have already applied to volunteer for this event.";

                    return RedirectToAction(nameof(MyApplications));
                }

                ModelState.AddModelError(
                    string.Empty,
                    "Your application could not be saved. Please try again.");

                model.Event = eventItem;

                return View(model);
            }

            TempData["SuccessMessage"] =
                "Your volunteer application has been submitted successfully.";

            return RedirectToAction(nameof(MyApplications));
        }


        [Authorize(Roles = AppRoles.Student)]
        public async Task<IActionResult> MyApplications()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var applications = await _context.Volunteers
                .AsNoTracking()
                .Include(v => v.Event)
                    .ThenInclude(e => e!.Venue)
                .Include(v => v.Event)
                    .ThenInclude(e => e!.Club)
                .Where(v => v.VolunteerId == userId)
                .OrderByDescending(v => v.Event!.StartDateTime)
                .ToListAsync();

            return View(applications);
        }

       

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.Student)]
        public async Task<IActionResult> Complete(int id)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            
            var volunteer = await _context.Volunteers
                .FirstOrDefaultAsync(v =>
                    v.Id == id &&
                    v.VolunteerId == userId);

            if (volunteer == null)
            {
                return NotFound();
            }

            if (volunteer.Status != VolunteerStatus.Accepted)
            {
                TempData["InfoMessage"] =
                    "Only an accepted volunteer assignment can be marked as completed.";

                return RedirectToAction(nameof(MyApplications));
            }

            volunteer.Status = VolunteerStatus.Completed;
            volunteer.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Your volunteer work has been marked as completed.";

            return RedirectToAction(nameof(MyApplications));
        }

    

        [Authorize]
        public async Task<IActionResult> Manage()
        {
            var rows = await _volunteerService.ManageableEvents(User)
                .AsNoTracking()
                .Where(e =>
                    e.ApprovalStatus == EventApprovalStatus.Approved ||
                    e.Volunteers.Any())
                .Select(e => new VolunteerManageRow
                {
                    EventId = e.Id,
                    Name = e.Name,
                    EventType = e.EventType,
                    ClubName = e.Club != null ? e.Club.Name : e.ClubName,
                    VenueName = e.Venue != null ? e.Venue.Name : null,
                    StartDateTime = e.StartDateTime,
                    EndDateTime = e.EndDateTime,
                    Pending = e.Volunteers.Count(v =>
                        v.Status == VolunteerStatus.Pending),
                    Accepted = e.Volunteers.Count(v =>
                        v.Status == VolunteerStatus.Accepted),
                    Completed = e.Volunteers.Count(v =>
                        v.Status == VolunteerStatus.Completed),
                    Rejected = e.Volunteers.Count(v =>
                        v.Status == VolunteerStatus.Rejected)
                })
                .ToListAsync();

            return View(rows);
        }

      
        [Authorize]
        public async Task<IActionResult> EventVolunteers(int eventId)
        {
            var eventItem = await _context.Events
                .AsNoTracking()
                .Include(e => e.Venue)
                .Include(e => e.Club)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (eventItem == null)
            {
                return NotFound();
            }

            if (!await _volunteerService.CanManageVolunteersAsync(eventId, User))
            {
                TempData["ErrorMessage"] = NotAuthorizedMessage;

                return RedirectToAction(nameof(Manage));
            }

            var volunteers = await _context.Volunteers
                .AsNoTracking()
                .Include(v => v.VolunteerUser)
                .Where(v => v.EventId == eventId)
                .OrderBy(v => v.VolunteerUser!.FullName)
                .ToListAsync();

            var model = new EventVolunteersViewModel
            {
                Event = eventItem,
                Pending = volunteers
                    .Where(v => v.Status == VolunteerStatus.Pending).ToList(),
                Accepted = volunteers
                    .Where(v => v.Status == VolunteerStatus.Accepted).ToList(),
                Completed = volunteers
                    .Where(v => v.Status == VolunteerStatus.Completed).ToList(),
                Rejected = volunteers
                    .Where(v => v.Status == VolunteerStatus.Rejected).ToList(),
            };

            return View(model);
        }

       

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Assign(int id)
        {
            var volunteer = await _context.Volunteers
                .FirstOrDefaultAsync(v => v.Id == id);

            if (volunteer == null)
            {
                return NotFound();
            }

            if (!await _volunteerService
                    .CanManageVolunteersAsync(volunteer.EventId, User))
            {
                TempData["ErrorMessage"] = NotAuthorizedMessage;

                return RedirectToAction(nameof(Manage));
            }

            if (volunteer.Status != VolunteerStatus.Pending)
            {
                TempData["InfoMessage"] =
                    "This volunteer application has already been processed.";

                return RedirectToAction(
                    nameof(EventVolunteers),
                    new { eventId = volunteer.EventId });
            }

           

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == volunteer.EventId);

            if (eventItem == null)
            {
                return NotFound();
            }

            var volunteerConflict =
                await _conflictDetectionService.FindVolunteerConflictAsync(
                    volunteer.VolunteerId,
                    eventItem.StartDateTime,
                    eventItem.EndDateTime,
                    eventItem.Id);

            if (volunteerConflict != null)
            {
                TempData["ErrorMessage"] =
                    $"Volunteer cannot be assigned because they are already " +
                    $"assigned to '{volunteerConflict.Event!.Name}' " +
                    $"from {volunteerConflict.Event.StartDateTime:g} " +
                    $"to {volunteerConflict.Event.EndDateTime:g}.";

                return RedirectToAction(
                    nameof(EventVolunteers),
                    new { eventId = volunteer.EventId });
            }

          

            volunteer.Status = VolunteerStatus.Accepted;
            volunteer.AssignedAt = DateTime.UtcNow;
            volunteer.CompletedAt = null;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Volunteer application approved.";

            return RedirectToAction(
                nameof(EventVolunteers),
                new { eventId = volunteer.EventId });
        }

       
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Reject(int id)
        {
            var volunteer = await _context.Volunteers
                .FirstOrDefaultAsync(v => v.Id == id);

            if (volunteer == null)
            {
                return NotFound();
            }

            if (!await _volunteerService
                    .CanManageVolunteersAsync(volunteer.EventId, User))
            {
                TempData["ErrorMessage"] = NotAuthorizedMessage;

                return RedirectToAction(nameof(Manage));
            }

            if (volunteer.Status != VolunteerStatus.Pending)
            {
                TempData["InfoMessage"] =
                    "This volunteer application has already been processed.";

                return RedirectToAction(
                    nameof(EventVolunteers),
                    new { eventId = volunteer.EventId });
            }

            volunteer.Status = VolunteerStatus.Rejected;
            volunteer.AssignedAt = null;
            volunteer.CompletedAt = null;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Volunteer application rejected.";

            return RedirectToAction(
                nameof(EventVolunteers),
                new { eventId = volunteer.EventId });
        }

       

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> AssignWork(AssignVolunteerWorkViewModel model)
        {
            if (!await _volunteerService.CanManageVolunteersAsync(model.EventId, User))
            {
                TempData["ErrorMessage"] = NotAuthorizedMessage;
                return RedirectToAction(nameof(Manage));
            }

            var volunteer = await _context.Volunteers
                .FirstOrDefaultAsync(v =>
                    v.Id == model.VolunteerId &&
                    v.EventId == model.EventId);

            if (volunteer == null)
            {
                return NotFound();
            }

            if (volunteer.Status != VolunteerStatus.Accepted)
            {
                TempData["ErrorMessage"] =
                    "Work can only be assigned to an accepted volunteer.";

                return RedirectToAction(
                    nameof(EventVolunteers),
                    new { eventId = model.EventId });
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] =
                    "Please enter the work to assign.";

                return RedirectToAction(
                    nameof(EventVolunteers),
                    new { eventId = model.EventId });
            }

            volunteer.Role = model.AssignedWork.Trim();
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Volunteer work assigned successfully.";

            return RedirectToAction(
                nameof(EventVolunteers),
                new { eventId = model.EventId });
        }

       

        private Task<Event?> LoadEventAsync(int eventId)
        {
            return _context.Events
                .AsNoTracking()
                .Include(e => e.Venue)
                .Include(e => e.Club)
                .FirstOrDefaultAsync(e => e.Id == eventId);
        }

       
        private async Task<IActionResult?> CheckApplyEligibilityAsync(
            Event? eventItem,
            string userId)
        {
            if (eventItem == null)
            {
                TempData["ErrorMessage"] =
                    "The selected event could not be found.";

                return RedirectToAction(nameof(Index));
            }

           
            if (!string.IsNullOrWhiteSpace(userId) &&
                eventItem.OrganizerId == userId)
            {
                TempData["ErrorMessage"] =
                    "As the organizer of this event you cannot apply to volunteer.";

                return RedirectToAction("Details", "Events", new { id = eventItem.Id });
            }

            if (eventItem.ApprovalStatus != EventApprovalStatus.Approved)
            {
                TempData["ErrorMessage"] =
                    "You can only volunteer for approved events.";

                return RedirectToAction(nameof(Index));
            }

            if (eventItem.HasEnded)
            {
                TempData["ErrorMessage"] =
                    "This event has already ended, so volunteer applications are closed.";

                return RedirectToAction(nameof(Index));
            }

            var alreadyApplied = await _context.Volunteers.AnyAsync(v =>
                v.EventId == eventItem.Id &&
                v.VolunteerId == userId);

            if (alreadyApplied)
            {
                TempData["InfoMessage"] =
                    "You have already applied to volunteer for this event.";

                return RedirectToAction(nameof(MyApplications));
            }

            var alreadyParticipant = await _context.Registrations.AnyAsync(r =>
                r.EventId == eventItem.Id &&
                r.UserId == userId);

            if (alreadyParticipant)
            {
                TempData["ErrorMessage"] =
                    "You are already registered as a participant for this event, so you cannot apply as a volunteer.";

                return RedirectToAction("Details", "Events", new { id = eventItem.Id });
            }

            return null;
        }
    }
}
