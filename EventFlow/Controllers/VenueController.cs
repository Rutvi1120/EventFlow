using EventFlow.Data;
using EventFlow.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Controllers
{
    [Authorize(Roles = "Admin")]
    public class VenueController : Controller
    {
        private readonly ApplicationDbContext _context;

        public VenueController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var venues = await _context.Venues
                .OrderBy(v => v.Name)
                .ToListAsync();

            return View(venues);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new Venue());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Venue model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _context.Venues.Add(model);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var venue = await _context.Venues
                .FindAsync(id);

            if (venue == null)
            {
                return NotFound();
            }

            return View(venue);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Venue model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var venue = await _context.Venues
                .FindAsync(id);

            if (venue == null)
            {
                return NotFound();
            }

            venue.Name = model.Name;
            venue.Location = model.Location;
            venue.Capacity = model.Capacity;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var venue = await _context.Venues
                .FindAsync(id);

            if (venue == null)
            {
                return NotFound();
            }

            var hasEvents = await _context.Events
                .AnyAsync(e => e.VenueId == id);

            ViewBag.HasEvents = hasEvents;

            return View(venue);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var venue = await _context.Venues
                .FindAsync(id);

            if (venue == null)
            {
                return NotFound();
            }

            var hasEvents = await _context.Events
                .AnyAsync(e => e.VenueId == id);

            if (hasEvents)
            {
                TempData["Error"] =
                    "This venue cannot be deleted because it is assigned to an event.";

                return RedirectToAction(nameof(Index));
            }

            _context.Venues.Remove(venue);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}