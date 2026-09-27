using EventFlow.Data;
using EventFlow.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Controllers
{
    [Authorize(Roles = "Participant")]
    public class ParticipantController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ParticipantController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // =========================
        // PARTICIPANT DASHBOARD
        // =========================
        public async Task<IActionResult> Dashboard()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
            {
                return Challenge();
            }

            var upcomingEvents = await _context.Events
                .Include(e => e.Venue)
                .Where(e => e.StartDateTime > DateTime.Now)
                .OrderBy(e => e.StartDateTime)
                .Take(5)
                .ToListAsync();

            ViewBag.TotalEvents =
                await _context.Events.CountAsync();

            ViewBag.UpcomingEvents =
                await _context.Events
                    .CountAsync(e => e.StartDateTime > DateTime.Now);

            ViewBag.MyRegistrations =
                await _context.Registrations
                    .CountAsync(r => r.UserId == currentUser.Id);

            ViewBag.MyWaitlist =
                await _context.WaitlistEntries
                    .CountAsync(w => w.UserId == currentUser.Id);

            return View(upcomingEvents);
        }
    }
}