using EventFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Controllers
{
    [Authorize(Roles = "Organizer")]
    public class OrganizerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrganizerController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            var totalEvents = await _context.Events.CountAsync();

            var upcomingEvents = await _context.Events
                .CountAsync(e => e.Status == "Upcoming");

            var ongoingEvents = await _context.Events
                .CountAsync(e => e.Status == "Ongoing");

            var delayedEvents = await _context.Events
                .CountAsync(e => e.Status == "Delayed");

            ViewBag.TotalEvents = totalEvents;
            ViewBag.UpcomingEvents = upcomingEvents;
            ViewBag.OngoingEvents = ongoingEvents;
            ViewBag.DelayedEvents = delayedEvents;

            var events = await _context.Events
                .Include(e => e.Venue)
                .OrderBy(e => e.StartDateTime)
                .Take(5)
                .ToListAsync();

            return View(events);
        }
    }
}