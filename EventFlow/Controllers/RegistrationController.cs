using EventFlow.Data;
using EventFlow.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Controllers
{
    [Authorize(Roles = "Participant")]
    public class RegistrationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public RegistrationController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Register for an event
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(int eventId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var eventModel = await _context.Events
                .Include(e => e.Venue)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (eventModel == null)
                return NotFound();

            // Check whether the user is already registered
            var alreadyRegistered = await _context.Registrations
                .AnyAsync(r =>
                    r.EventId == eventId &&
                    r.UserId == user.Id);

            if (alreadyRegistered)
            {
                TempData["Message"] = "You are already registered for this event.";
                return RedirectToAction(
                    "Details",
                    "Events",
                    new { id = eventId });
            }

            // Check whether the user is already on the waitlist
            var alreadyWaitlisted = await _context.WaitlistEntries
                .AnyAsync(w =>
                    w.EventId == eventId &&
                    w.UserId == user.Id);

            if (alreadyWaitlisted)
            {
                TempData["Message"] = "You are already on the waitlist for this event.";
                return RedirectToAction(
                    "Details",
                    "Events",
                    new { id = eventId });
            }

            // Count current registrations
            var registrationCount = await _context.Registrations
                .CountAsync(r => r.EventId == eventId);

            // Event has available seats
            if (registrationCount < eventModel.MaxParticipants)
            {
                var registration = new Registration
                {
                    EventId = eventId,
                    UserId = user.Id,
                    RegisteredAt = DateTime.Now
                };

                _context.Registrations.Add(registration);
                await _context.SaveChangesAsync();

                TempData["Message"] =
                    "You have successfully registered for this event.";
            }
            else
            {
                // Event is full, so add user to waitlist
                var waitlistEntry = new WaitlistEntry
                {
                    EventId = eventId,
                    UserId = user.Id,
                    JoinedAt = DateTime.Now
                };

                _context.WaitlistEntries.Add(waitlistEntry);
                await _context.SaveChangesAsync();

                TempData["Message"] =
                    "The event is full. You have been added to the waitlist.";
            }

            return RedirectToAction(
                "Details",
                "Events",
                new { id = eventId });
        }

        // Cancel registration
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRegistration(int eventId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var registration = await _context.Registrations
                .FirstOrDefaultAsync(r =>
                    r.EventId == eventId &&
                    r.UserId == user.Id);

            if (registration == null)
            {
                TempData["Message"] =
                    "You are not registered for this event.";

                return RedirectToAction(
                    "Details",
                    "Events",
                    new { id = eventId });
            }

            _context.Registrations.Remove(registration);
            await _context.SaveChangesAsync();

            // Move the first waitlisted participant into the event
            var nextWaitlist = await _context.WaitlistEntries
                .Where(w => w.EventId == eventId)
                .OrderBy(w => w.JoinedAt)
                .FirstOrDefaultAsync();

            if (nextWaitlist != null)
            {
                var newRegistration = new Registration
                {
                    EventId = nextWaitlist.EventId,
                    UserId = nextWaitlist.UserId,
                    RegisteredAt = DateTime.Now
                };

                _context.Registrations.Add(newRegistration);
                _context.WaitlistEntries.Remove(nextWaitlist);

                await _context.SaveChangesAsync();
            }

            TempData["Message"] =
                "Your registration has been cancelled.";

            return RedirectToAction(
                "Details",
                "Events",
                new { id = eventId });
        }

        // Leave waitlist
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LeaveWaitlist(int eventId)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var waitlistEntry = await _context.WaitlistEntries
                .FirstOrDefaultAsync(w =>
                    w.EventId == eventId &&
                    w.UserId == user.Id);

            if (waitlistEntry == null)
            {
                TempData["Message"] =
                    "You are not on the waitlist for this event.";

                return RedirectToAction(
                    "Details",
                    "Events",
                    new { id = eventId });
            }

            _context.WaitlistEntries.Remove(waitlistEntry);
            await _context.SaveChangesAsync();

            TempData["Message"] =
                "You have been removed from the waitlist.";

            return RedirectToAction(
                "Details",
                "Events",
                new { id = eventId });
        }

        // My registrations
        [HttpGet]
        public async Task<IActionResult> MyRegistrations()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var registrations = await _context.Registrations
                .Include(r => r.Event)
                .ThenInclude(e => e!.Venue)
                .Where(r => r.UserId == user.Id)
                .OrderByDescending(r => r.RegisteredAt)
                .ToListAsync();

            return View(registrations);
        }

        // My waitlist entries
        [HttpGet]
        public async Task<IActionResult> MyWaitlist()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Challenge();

            var waitlist = await _context.WaitlistEntries
                .Include(w => w.Event)
                .ThenInclude(e => e!.Venue)
                .Where(w => w.UserId == user.Id)
                .OrderBy(w => w.JoinedAt)
                .ToListAsync();

            return View(waitlist);
        }
    }
}