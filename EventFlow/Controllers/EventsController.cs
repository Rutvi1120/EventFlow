using EventFlow.Data;
using EventFlow.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Controllers
{
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EventsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var events = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.Organizer)
                .Include(e => e.Club)
                .Where(e => e.ApprovalStatus == "Approved")
                .OrderBy(e => e.StartDateTime)
                .ToListAsync();

            return View(events);
        }


        [AllowAnonymous]
        public async Task<IActionResult> Details(int id)
        {
            var eventItem = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.Organizer)
                .Include(e => e.Club)
                .Include(e => e.Registrations)
                .Include(e => e.WaitlistEntries)
                .Include(e => e.Volunteers)
                .FirstOrDefaultAsync(e =>
                    e.Id == id &&
                    e.ApprovalStatus == "Approved");

            if (eventItem == null)
            {
                return NotFound();
            }

            // Registration information
            ViewBag.RegisteredCount = eventItem.Registrations.Count;

            ViewBag.SeatsLeft = Math.Max(
                0,
                eventItem.MaxParticipants -
                eventItem.Registrations.Count);

            // Current user
            var userId = _userManager.GetUserId(User);

            // Check whether the current user is the event creator
            ViewBag.IsOrganizer =
                !string.IsNullOrWhiteSpace(userId) &&
                userId == eventItem.OrganizerId;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                ViewBag.IsRegistered = eventItem.Registrations
                    .Any(r => r.UserId == userId);

                ViewBag.IsWaitlisted = eventItem.WaitlistEntries
                    .Any(w => w.UserId == userId);

                var myVolunteer = eventItem.Volunteers
                    .FirstOrDefault(v =>
                        v.VolunteerId == userId &&
                        v.Status != VolunteerStatus.Rejected);

                ViewBag.MyVolunteerStatus = myVolunteer?.Status;
                ViewBag.IsActiveVolunteer = myVolunteer != null;
            }

            // Waitlist count
            ViewBag.WaitlistCount =
                eventItem.WaitlistEntries.Count;

            return View(eventItem);
        }
        [Authorize]
        public async Task<IActionResult> MyEvents()
        {
            var userId = User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

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
    }
}