using EventFlow.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Controllers
{
    [Authorize(Roles = "Participant")]
    public class ParticipantController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ParticipantController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            var upcomingEvents = await _context.Events
                .Include(e => e.Venue)
                .Where(e => e.StartDateTime > DateTime.Now)
                .OrderBy(e => e.StartDateTime)
                .Take(5)
                .ToListAsync();

            ViewBag.TotalEvents = await _context.Events.CountAsync();

            ViewBag.UpcomingEvents = await _context.Events
                .CountAsync(e => e.StartDateTime > DateTime.Now);

            return View(upcomingEvents);
        }
    }
}