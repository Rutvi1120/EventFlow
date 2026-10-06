using EventFlow.Data;
using EventFlow.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

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

           
            ViewBag.RegisteredCount = eventItem.Registrations.Count;

            ViewBag.SeatsLeft = Math.Max(
                0,
                eventItem.MaxParticipants -
                eventItem.Registrations.Count);

            
            var userId = _userManager.GetUserId(User);


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

           
            ViewBag.WaitlistCount =
                eventItem.WaitlistEntries.Count;

            return View(eventItem);
        }

        [Authorize]
        public async Task<IActionResult> Participants(int id)
        {
            var eventItem = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.Organizer)
                .Include(e => e.Registrations)
                    .ThenInclude(r => r.User)
                .FirstOrDefaultAsync(e => e.Id == id && e.ApprovalStatus == "Approved");

            if (eventItem == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);

           
            if (string.IsNullOrWhiteSpace(userId) ||
                (eventItem.OrganizerId != userId && !User.IsInRole("Admin")))
            {
                return Forbid();
            }

            return View(eventItem);
        }

        [Authorize]
        public async Task<IActionResult> ExportParticipants(int id)
        {
            var eventItem = await _context.Events
                .Include(e => e.Registrations)
                    .ThenInclude(r => r.User)
                .FirstOrDefaultAsync(e => e.Id == id && e.ApprovalStatus == "Approved");

            if (eventItem == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId) ||
                (eventItem.OrganizerId != userId && !User.IsInRole("Admin")))
            {
                return Forbid();
            }

            var sb = new StringBuilder();
            sb.AppendLine("Name,Email,RegisteredAt,EventName");

            foreach (var r in eventItem.Registrations.OrderBy(r => r.RegisteredAt))
            {
                var name = r.User?.FullName ?? string.Empty;
                var email = r.User?.Email ?? string.Empty;
                var date = r.RegisteredAt.ToString("u");
                sb.AppendLine($"\"{name}\",\"{email}\",\"{date}\",\"{eventItem.Name}\"");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());

            return File(bytes, "text/csv", $"participants_event_{id}.csv");
        }

        [Authorize]
        public async Task<IActionResult> ExportVolunteers(int id)
        {
            var eventItem = await _context.Events
                .Include(e => e.Volunteers)
                    .ThenInclude(v => v.VolunteerUser)
                .FirstOrDefaultAsync(e => e.Id == id && e.ApprovalStatus == "Approved");

            if (eventItem == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId) ||
                (eventItem.OrganizerId != userId && !User.IsInRole("Admin")))
            {
                return Forbid();
            }

            var sb = new StringBuilder();
            sb.AppendLine("Name,Email,Status,AppliedAt,EventName");

            foreach (var v in eventItem.Volunteers.OrderBy(v => v.Id))
            {
                var name = v.VolunteerUser?.FullName ?? string.Empty;
                var email = v.VolunteerUser?.Email ?? string.Empty;
                var status = v.Status ?? string.Empty;
                var applied = v.AssignedAt?.ToString("u") ?? string.Empty;
                sb.AppendLine($"\"{name}\",\"{email}\",\"{status}\",\"{applied}\",\"{eventItem.Name}\"");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());

            return File(bytes, "text/csv", $"volunteers_event_{id}.csv");
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