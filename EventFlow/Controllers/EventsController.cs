using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventFlow.Models;
using EventFlow.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;

//[Authorize(Roles = "Admin,Organizer")]
public class EventsController : Controller
{
    private readonly ApplicationDbContext _context;

    public EventsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Events
    public async Task<IActionResult> Index()
    {
        return View(await _context.Events
            .Include(e => e.Venue)
            .ToListAsync());
    }

    // GET: Events/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var eventModel = await _context.Events
            .Include(e => e.Venue)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (eventModel == null)
        {
            return NotFound();
        }

        return View(eventModel);
    }

    // GET: Events/Create
    public IActionResult Create()
    {
        ViewData["VenueId"] = new SelectList(
            _context.Venues,
            "Id",
            "Name");

        return View();
    }

    // POST: Events/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Id,Name,Description,StartDateTime,EndDateTime,MaxParticipants,Status,VenueId")]
        Event eventModel)
    {
        // Check end date and time
        if (eventModel.EndDateTime <= eventModel.StartDateTime)
        {
            ModelState.AddModelError(
                "EndDateTime",
                "End date and time must be after start date and time.");
        }

        // Check venue and capacity
        var venue = await _context.Venues
            .FindAsync(eventModel.VenueId);

        if (venue == null)
        {
            ModelState.AddModelError(
                "VenueId",
                "Please select a valid venue.");
        }
        else if (eventModel.MaxParticipants > venue.Capacity)
        {
            ModelState.AddModelError(
                "MaxParticipants",
                $"Maximum participants cannot exceed the venue capacity of {venue.Capacity}.");
        }

        if (ModelState.IsValid)
        {
            eventModel.Status = "Upcoming";

            _context.Events.Add(eventModel);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        ViewData["VenueId"] = new SelectList(
            _context.Venues,
            "Id",
            "Name",
            eventModel.VenueId);

        return View(eventModel);
    }

    // GET: Events/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var eventModel = await _context.Events.FindAsync(id);

        if (eventModel == null)
        {
            return NotFound();
        }

        ViewData["VenueId"] = new SelectList(
            _context.Venues,
            "Id",
            "Name",
            eventModel.VenueId);

        return View(eventModel);
    }

    // POST: Events/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int? id,
        [Bind("Id,Name,Description,StartDateTime,EndDateTime,MaxParticipants,Status,VenueId")]
        Event eventModel)
    {
        if (id != eventModel.Id)
        {
            return NotFound();
        }

        // Check end date and time
        if (eventModel.EndDateTime <= eventModel.StartDateTime)
        {
            ModelState.AddModelError(
                "EndDateTime",
                "End date and time must be after start date and time.");
        }

        // Check venue and capacity
        var venue = await _context.Venues
            .FindAsync(eventModel.VenueId);

        if (venue == null)
        {
            ModelState.AddModelError(
                "VenueId",
                "Please select a valid venue.");
        }
        else if (eventModel.MaxParticipants > venue.Capacity)
        {
            ModelState.AddModelError(
                "MaxParticipants",
                $"Maximum participants cannot exceed the venue capacity of {venue.Capacity}.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Events.Update(eventModel);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EventExists(eventModel.Id))
                {
                    return NotFound();
                }

                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        ViewData["VenueId"] = new SelectList(
            _context.Venues,
            "Id",
            "Name",
            eventModel.VenueId);

        return View(eventModel);
    }

    // GET: Events/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var eventModel = await _context.Events
            .Include(e => e.Venue)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (eventModel == null)
        {
            return NotFound();
        }

        return View(eventModel);
    }

    // POST: Events/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var eventModel = await _context.Events.FindAsync(id);

        if (eventModel != null)
        {
            _context.Events.Remove(eventModel);
        }

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private bool EventExists(int id)
    {
        return _context.Events.Any(e => e.Id == id);
    }
}