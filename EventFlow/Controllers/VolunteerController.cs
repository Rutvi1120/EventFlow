using EventFlow.Data;
using EventFlow.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Controllers
{
    [Authorize(Roles = "Volunteer")]
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

        // GET: Volunteer/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
                return Challenge();

            var assignments = await _context.Volunteers
                .Include(v => v.Event)
                .ThenInclude(e => e.Venue)
                .Where(v => v.VolunteerId == currentUser.Id)
                .OrderBy(v => v.Event.StartDateTime)
                .ToListAsync();

            ViewBag.TotalAssignments = assignments.Count;

            ViewBag.ActiveAssignments = assignments.Count(v =>
                v.Status == "Assigned" ||
                v.Status == "Accepted");

            ViewBag.CompletedAssignments = assignments.Count(v =>
                v.Status == "Completed");

            ViewBag.UpcomingAssignments = assignments.Count(v =>
                v.Event.StartDateTime > DateTime.Now);

            return View(assignments);
        }


        // GET: Volunteer/Assignment
        public async Task<IActionResult> Assignment()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
                return Challenge();

            var assignments = await _context.Volunteers
                .Include(v => v.Event)
                .ThenInclude(e => e.Venue)
                .Where(v => v.VolunteerId == currentUser.Id)
                .OrderBy(v => v.Event.StartDateTime)
                .ToListAsync();

            return View(assignments);
        }


        // POST: Volunteer/AcceptAssignment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptAssignment(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var assignment = await _context.Volunteers
                .FirstOrDefaultAsync(v =>
                    v.Id == id &&
                    v.VolunteerId == user.Id);

            if (assignment == null)
                return NotFound();

            if (assignment.Status != "Assigned")
            {
                TempData["Error"] =
                    "This assignment can no longer be accepted.";

                return RedirectToAction(nameof(Assignment));
            }

            assignment.Status = "Accepted";

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Volunteer assignment accepted successfully.";

            return RedirectToAction(nameof(Assignment));
        }


        // POST: Volunteer/RejectAssignment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectAssignment(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var assignment = await _context.Volunteers
                .FirstOrDefaultAsync(v =>
                    v.Id == id &&
                    v.VolunteerId == user.Id);

            if (assignment == null)
                return NotFound();

            if (assignment.Status != "Assigned")
            {
                TempData["Error"] =
                    "This assignment can no longer be rejected.";

                return RedirectToAction(nameof(Assignment));
            }

            assignment.Status = "Rejected";

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Volunteer assignment rejected.";

            return RedirectToAction(nameof(Assignment));
        }


        // GET: Volunteer/Details/5
        // GET: Volunteer/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
                return Challenge();

            var assignment = await _context.Volunteers
                .Include(v => v.Event)
                .ThenInclude(e => e.Venue)
                .Include(v => v.VolunteerUser)
                .FirstOrDefaultAsync(v =>
                    v.Id == id &&
                    v.VolunteerId == currentUser.Id);

            if (assignment == null)
            {
                TempData["Error"] = "Volunteer assignment not found.";
                return RedirectToAction(nameof(Assignment));
            }

            return View("Details", assignment);
        }


        // POST: Volunteer/MarkCompleted
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkCompleted(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
                return Challenge();

            var assignment = await _context.Volunteers
                .FirstOrDefaultAsync(v =>
                    v.Id == id &&
                    v.VolunteerId == currentUser.Id);

            if (assignment == null)
                return NotFound();

            if (assignment.Status != "Accepted")
            {
                TempData["Error"] =
                    "Only accepted assignments can be marked as completed.";

                return RedirectToAction(
                    nameof(Details),
                    new { id });
            }

            assignment.Status = "Completed";
            assignment.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Assignment marked as completed successfully.";

            return RedirectToAction(
                nameof(Details),
                new { id });
        }
    }
}