
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventFlow.Models;
using EventFlow.Data;
using Microsoft.AspNetCore.Authorization;

//[Authorize(Roles = "Admin,Organizer")]
public class VenuesController : Controller
{
    private readonly ApplicationDbContext _context;

    public VenuesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: VENUES
    public async Task<IActionResult> Index()    
    {
        return View(await _context.Venues.ToListAsync());
    }

    // GET: VENUES/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var venue = await _context.Venues
            .FirstOrDefaultAsync(m => m.Id == id);
        if (venue == null)
        {
            return NotFound();
        }

        return View(venue);
    }

    // GET: VENUES/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: VENUES/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Name,Location,Capacity")] Venue venue)
    {
        if (ModelState.IsValid)
        {
            _context.Add(venue);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(venue);
    }

    // GET: VENUES/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var venue = await _context.Venues.FindAsync(id);
        if (venue == null)
        {
            return NotFound();
        }
        return View(venue);
    }

    // POST: VENUES/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Name,Location,Capacity")] Venue venue)
    {
        if (id != venue.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(venue);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!VenueExists(venue.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(venue);
    }

    // GET: VENUES/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var venue = await _context.Venues
            .FirstOrDefaultAsync(m => m.Id == id);
        if (venue == null)
        {
            return NotFound();
        }

        return View(venue);
    }

    // POST: VENUES/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var venue = await _context.Venues.FindAsync(id);
        if (venue != null)
        {
            _context.Venues.Remove(venue);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool VenueExists(int? id)
    {
        return _context.Venues.Any(e => e.Id == id);
    }
}
