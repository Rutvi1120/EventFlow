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

        public VolunteerController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            VolunteerManagementService volunteerService)
        {
            _context = context;
            _userManager = userManager;
            _volunteerService = volunteerService;
        }

        // =====================================================
        // STUDENT - BROWSE APPROVED UPCOMING EVENTS
        // =====================================================

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

            // EventId -> my status, so the view can show "Applied" instead of "Apply".
            ViewBag.MyApplications = await _context.Volunteers
                .AsNoTracking()
                .Where(v => v.VolunteerId == userId)
                .ToDictionaryAsync(v => v.EventId, v => v.Status);

            return View(events);
        }

        // =====================================================
        // STUDENT - APPLY (GET)
        // =====================================================

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

        // =====================================================
        // STUDENT - APPLY (POST)
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.Student)]
        public async Task<IActionResult> Apply(VolunteerApplyViewModel model)
        {
            // The student's identity ALWAYS comes from the login, never the form.
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
                // Show the real validation errors on the same page.
                model.Event = eventItem;

                return View(model);
            }

            var volunteer = new Volunteer
            {
                EventId = eventItem!.Id,
                VolunteerId = userId,
                Role = model.Role.Trim(),
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
                // The unique (EventId, VolunteerId) index rejected a double submit.
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

        // =====================================================
        // STUDENT - MY APPLICATIONS
        // =====================================================

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

        // =====================================================
        // STUDENT - MARK COMPLETED
        // =====================================================

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

            // Filtering by the logged-in user means a student can never
            // complete somebody else's assignment (IDOR protection).
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

        // =====================================================
        // ADMIN / FACULTY / CLUB PRESIDENT - EVENT LIST
        // =====================================================

        [Authorize(Roles = AppRoles.VolunteerManagers)]
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

        // =====================================================
        // ADMIN / FACULTY / CLUB PRESIDENT - EVENT VOLUNTEERS
        // =====================================================

        [Authorize(Roles = AppRoles.VolunteerManagers)]
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
                CanAddVolunteer =
                    VolunteerManagementService.CanAddDirectly(User) &&
                    eventItem.IsApproved &&
                    !eventItem.HasEnded,
                AddVolunteerController = User.IsInRole(AppRoles.Faculty)
                    ? "Faculty"
                    : "ClubPresident"
            };

            return View(model);
        }

        // =====================================================
        // APPROVE APPLICATION  (Pending -> Accepted)
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.VolunteerManagers)]
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

            volunteer.Status = VolunteerStatus.Accepted;
            volunteer.AssignedAt = DateTime.UtcNow;
            volunteer.CompletedAt = null;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Volunteer application approved.";

            return RedirectToAction(
                nameof(EventVolunteers),
                new { eventId = volunteer.EventId });
        }

        // =====================================================
        // REJECT APPLICATION  (Pending -> Rejected)
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppRoles.VolunteerManagers)]
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

        // =====================================================
        // HELPERS
        // =====================================================

        private Task<Event?> LoadEventAsync(int eventId)
        {
            return _context.Events
                .AsNoTracking()
                .Include(e => e.Venue)
                .Include(e => e.Club)
                .FirstOrDefaultAsync(e => e.Id == eventId);
        }

        /// <summary>
        /// Server-side rules for a student volunteer application.
        /// Returns a redirect (with a TempData message) when the student
        /// may NOT apply, or null when everything is fine.
        /// </summary>
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

            return null;
        }
    }
}
