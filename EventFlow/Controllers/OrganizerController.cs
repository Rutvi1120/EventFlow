using EventFlow.Data;
using EventFlow.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Controllers
{
    [Authorize(Roles = "Organizer")]
    public class OrganizerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrganizerController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // =========================
        // ORGANIZER DASHBOARD
        // =========================
        public async Task<IActionResult> Dashboard()
        {
            var totalEvents = await _context.Events.CountAsync();

            var upcomingEvents = await _context.Events
                .CountAsync(e => e.Status == "Upcoming");

            var ongoingEvents = await _context.Events
                .CountAsync(e => e.Status == "Ongoing");

            var delayedEvents = await _context.Events
                .CountAsync(e => e.Status == "Delayed");

            var totalVolunteerAssignments = await _context.Volunteers
                .CountAsync();

            ViewBag.TotalEvents = totalEvents;
            ViewBag.UpcomingEvents = upcomingEvents;
            ViewBag.OngoingEvents = ongoingEvents;
            ViewBag.DelayedEvents = delayedEvents;
            ViewBag.TotalVolunteerAssignments = totalVolunteerAssignments;

            var events = await _context.Events
                .Include(e => e.Venue)
                .OrderBy(e => e.StartDateTime)
                .Take(5)
                .ToListAsync();

            return View(events);
        }


        // =========================
        // MANAGE VOLUNTEERS
        // =========================
        [HttpGet]
        public async Task<IActionResult> ManageVolunteers()
        {
            // Get all users who have Volunteer role
            var users = await _userManager.GetUsersInRoleAsync("Volunteer");

            // Get all events
            var events = await _context.Events
                .OrderBy(e => e.StartDateTime)
                .ToListAsync();

            // Send data to View
            ViewBag.Volunteers = users;
            ViewBag.Events = events;

            // Existing assignments
            var assignments = await _context.Volunteers
                .Include(v => v.Event)
                .OrderByDescending(v => v.AssignedAt)
                .ToListAsync();

            return View(assignments);
        }


        // =========================
        // ASSIGN VOLUNTEER
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignVolunteer(
            string volunteerId,
            int eventId,
            string role,
            string? notes)
        {
            // Check volunteer
            var volunteer = await _userManager.FindByIdAsync(volunteerId);

            if (volunteer == null)
            {
                ModelState.AddModelError("", "Volunteer not found.");
                return RedirectToAction(nameof(ManageVolunteers));
            }

            // Make sure selected user actually has Volunteer role
            if (!await _userManager.IsInRoleAsync(volunteer, "Volunteer"))
            {
                ModelState.AddModelError("", "Selected user is not a Volunteer.");
                return RedirectToAction(nameof(ManageVolunteers));
            }

            // Check event
            var eventModel = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (eventModel == null)
            {
                ModelState.AddModelError("", "Event not found.");
                return RedirectToAction(nameof(ManageVolunteers));
            }

            // Check role
            if (string.IsNullOrWhiteSpace(role))
            {
                ModelState.AddModelError("", "Volunteer role is required.");
                return RedirectToAction(nameof(ManageVolunteers));
            }

            // Prevent assigning same volunteer to same event twice
            var alreadyAssigned = await _context.Volunteers
                .AnyAsync(v =>
                    v.VolunteerId == volunteerId &&
                    v.EventId == eventId);

            if (alreadyAssigned)
            {
                TempData["Error"] =
                    "This volunteer is already assigned to this event.";

                return RedirectToAction(nameof(ManageVolunteers));
            }

            // Create assignment
            var assignment = new Volunteer
            {
                VolunteerId = volunteerId,
                EventId = eventId,
                Role = role,
                Notes = notes,
                Status = "Assigned",
                AssignedAt = DateTime.UtcNow
            };

            _context.Volunteers.Add(assignment);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Volunteer assigned successfully.";

            return RedirectToAction(nameof(ManageVolunteers));
        }


        // =========================
        // REMOVE VOLUNTEER ASSIGNMENT
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveVolunteerAssignment(int id)
        {
            var assignment = await _context.Volunteers
                .FirstOrDefaultAsync(v => v.Id == id);

            if (assignment == null)
            {
                return NotFound();
            }

            _context.Volunteers.Remove(assignment);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Volunteer assignment removed successfully.";

            return RedirectToAction(nameof(ManageVolunteers));
        }
    }
}