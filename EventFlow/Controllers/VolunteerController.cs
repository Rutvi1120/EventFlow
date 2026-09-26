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
                return NotFound();

            return View(assignment);
        }

        // POST: Volunteer/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(
            int id,
            string status)
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

            var allowedStatuses = new[]
            {
                "Assigned",
                "Accepted",
                "Completed"
            };

            if (!allowedStatuses.Contains(status))
            {
                TempData["Error"] = "Invalid assignment status.";

                return RedirectToAction(
                    nameof(Details),
                    new { id });
            }

            assignment.Status = status;

            if (status == "Completed")
            {
                assignment.CompletedAt = DateTime.UtcNow;
            }
            else
            {
                assignment.CompletedAt = null;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Assignment status updated successfully.";

            return RedirectToAction(
                nameof(Details),
                new { id });
        }
    }
}