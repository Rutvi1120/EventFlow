using EventFlow.Data;
using EventFlow.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Controllers
{
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EventsController(ApplicationDbContext context)
        {
            _context = context;
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