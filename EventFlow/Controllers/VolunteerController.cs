using EventFlow.Data;
using EventFlow.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Controllers
{
    [Authorize]
    public class VolunteerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public VolunteerController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var events = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.Club)
                .Where(e =>
                    e.ApprovalStatus == "Approved" &&
                    e.EndDateTime >= DateTime.Now)
                .OrderBy(e => e.StartDateTime)
                .ToListAsync();

            return View(events);
        }

        [HttpGet]
        public async Task<IActionResult> Apply(int eventId)
        {
            var eventItem = await _context.Events
                .Include(e => e.Venue)
                .FirstOrDefaultAsync(e =>
                    e.Id == eventId &&
                    e.ApprovalStatus == "Approved");

            if (eventItem == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var existingApplication = await _context.Volunteers
                .AnyAsync(v =>
                    v.EventId == eventId &&
                    v.VolunteerId == userId);

            if (existingApplication)
            {
                return RedirectToAction(nameof(MyApplications));
            }

            ViewBag.Event = eventItem;

            var model = new Volunteer
            {
                EventId = eventId,
                VolunteerId = userId,
                Status = "Pending"
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(Volunteer model)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e =>
                    e.Id == model.EventId &&
                    e.ApprovalStatus == "Approved");

            if (eventItem == null)
            {
                return NotFound();
            }

            var existingApplication = await _context.Volunteers
                .AnyAsync(v =>
                    v.EventId == model.EventId &&
                    v.VolunteerId == userId);

            if (existingApplication)
            {
                return RedirectToAction(nameof(MyApplications));
            }

            model.VolunteerId = userId;
            model.Status = "Pending";
            model.AssignedAt = null;
            model.CompletedAt = null;

            if (!ModelState.IsValid)
            {
                ViewBag.Event = eventItem;
                return View(model);
            }

            _context.Volunteers.Add(model);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(MyApplications));
        }

        public async Task<IActionResult> MyApplications()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var applications = await _context.Volunteers
                .Include(v => v.Event)
                .ThenInclude(e => e!.Venue)
                .Where(v => v.VolunteerId == userId)
                .OrderByDescending(v => v.Event!.StartDateTime)
                .ToListAsync();

            return View(applications);
        }

        [Authorize(Roles = "Admin,Faculty,ClubPresident")]
        public async Task<IActionResult> EventVolunteers(int eventId)
        {
            var eventItem = await _context.Events
                .Include(e => e.Club)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (eventItem == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Challenge();
            }

            if (User.IsInRole("Faculty"))
            {
                var isSupervisor = await _context.Clubs
                    .AnyAsync(c =>
                        c.Id == eventItem.ClubId &&
                        c.FacultySupervisorId == currentUserId);

                var isCollegeEvent = eventItem.EventType == "College";

                if (!isSupervisor && !isCollegeEvent)
                {
                    return Forbid();
                }
            }

            if (User.IsInRole("ClubPresident"))
            {
                var isClubPresident = await _context.Clubs
                    .AnyAsync(c =>
                        c.Id == eventItem.ClubId &&
                        c.ClubPresidentId == currentUserId);

                if (!isClubPresident)
                {
                    return Forbid();
                }
            }

            var volunteers = await _context.Volunteers
                .Include(v => v.VolunteerUser)
                .Where(v => v.EventId == eventId)
                .OrderBy(v => v.Status)
                .ThenBy(v => v.VolunteerUser!.FullName)
                .ToListAsync();

            ViewBag.Event = eventItem;

            return View(volunteers);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Faculty,ClubPresident")]
        public async Task<IActionResult> Assign(int id)
        {
            var volunteer = await _context.Volunteers
                .Include(v => v.Event)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (volunteer == null)
            {
                return NotFound();
            }

            if (!await CanManageEventVolunteers(volunteer.Event))
            {
                return Forbid();
            }

            volunteer.Status = "Assigned";
            volunteer.AssignedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(EventVolunteers),
                new { eventId = volunteer.EventId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Faculty,ClubPresident")]
        public async Task<IActionResult> Reject(int id)
        {
            var volunteer = await _context.Volunteers
                .Include(v => v.Event)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (volunteer == null)
            {
                return NotFound();
            }

            if (!await CanManageEventVolunteers(volunteer.Event))
            {
                return Forbid();
            }

            volunteer.Status = "Rejected";
            volunteer.AssignedAt = null;

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(EventVolunteers),
                new { eventId = volunteer.EventId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(int id)
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

            if (volunteer.Status != "Assigned")
            {
                return BadRequest();
            }

            volunteer.Status = "Accepted";

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(MyApplications));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id)
        {
            var volunteer = await _context.Volunteers
                .Include(v => v.Event)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (volunteer == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var canManage = await CanManageEventVolunteers(volunteer.Event);

            if (volunteer.VolunteerId != userId && !canManage)
            {
                return Forbid();
            }

            if (volunteer.Status != "Accepted")
            {
                return BadRequest();
            }

            volunteer.Status = "Completed";
            volunteer.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            if (volunteer.VolunteerId == userId)
            {
                return RedirectToAction(nameof(MyApplications));
            }

            return RedirectToAction(
                nameof(EventVolunteers),
                new { eventId = volunteer.EventId });
        }

        private async Task<bool> CanManageEventVolunteers(Event? eventItem)
        {
            if (eventItem == null)
            {
                return false;
            }

            if (User.IsInRole("Admin"))
            {
                return true;
            }

            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            if (User.IsInRole("Faculty"))
            {
                if (eventItem.EventType == "College")
                {
                    return true;
                }

                if (eventItem.ClubId.HasValue)
                {
                    return await _context.Clubs.AnyAsync(c =>
                        c.Id == eventItem.ClubId.Value &&
                        c.FacultySupervisorId == userId);
                }
            }

            if (User.IsInRole("ClubPresident") &&
                eventItem.ClubId.HasValue)
            {
                return await _context.Clubs.AnyAsync(c =>
                    c.Id == eventItem.ClubId.Value &&
                    c.ClubPresidentId == userId);
            }

            return false;
        }
    }
}