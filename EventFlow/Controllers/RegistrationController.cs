using EventFlow.Data;
using EventFlow.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Controllers
{
    [Authorize]
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(int eventId)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e =>
                    e.Id == eventId &&
                    e.ApprovalStatus == "Approved");

            if (eventItem == null)
            {
                return NotFound();
            }

            if (eventItem.EndDateTime <= DateTime.Now)
            {
                return BadRequest();
            }

            var alreadyRegistered = await _context.Registrations
                .AnyAsync(r =>
                    r.EventId == eventId &&
                    r.UserId == userId);

            if (alreadyRegistered)
            {
                return RedirectToAction(
                    "Details",
                    "Events",
                    new { id = eventId });
            }

            var alreadyWaitlisted = await _context.WaitlistEntries
                .AnyAsync(w =>
                    w.EventId == eventId &&
                    w.UserId == userId);

            if (alreadyWaitlisted)
            {
                return RedirectToAction(
                    "Details",
                    "Events",
                    new { id = eventId });
            }

            var registrationCount = await _context.Registrations
                .CountAsync(r => r.EventId == eventId);

            if (registrationCount >= eventItem.MaxParticipants)
            {
                return RedirectToAction(
                    nameof(JoinWaitlist),
                    new { eventId });
            }

            _context.Registrations.Add(new Registration
            {
                EventId = eventId,
                UserId = userId,
                RegisteredAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return RedirectToAction(
                "Details",
                "Events",
                new { id = eventId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int eventId)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var registration = await _context.Registrations
                .FirstOrDefaultAsync(r =>
                    r.EventId == eventId &&
                    r.UserId == userId);

            if (registration == null)
            {
                return RedirectToAction(
                    "Details",
                    "Events",
                    new { id = eventId });
            }

            _context.Registrations.Remove(registration);

            await _context.SaveChangesAsync();

            await PromoteWaitlistedUser(eventId);

            return RedirectToAction(
                "Details",
                "Events",
                new { id = eventId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> JoinWaitlist(int eventId)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e =>
                    e.Id == eventId &&
                    e.ApprovalStatus == "Approved");

            if (eventItem == null)
            {
                return NotFound();
            }

            if (eventItem.EndDateTime <= DateTime.Now)
            {
                return BadRequest();
            }

            var registrationExists = await _context.Registrations
                .AnyAsync(r =>
                    r.EventId == eventId &&
                    r.UserId == userId);

            if (registrationExists)
            {
                return RedirectToAction(
                    "Details",
                    "Events",
                    new { id = eventId });
            }

            var waitlistExists = await _context.WaitlistEntries
                .AnyAsync(w =>
                    w.EventId == eventId &&
                    w.UserId == userId);

            if (waitlistExists)
            {
                return RedirectToAction(
                    "Details",
                    "Events",
                    new { id = eventId });
            }

            var registrationCount = await _context.Registrations
                .CountAsync(r => r.EventId == eventId);

            if (registrationCount < eventItem.MaxParticipants)
            {
                return RedirectToAction(
                    nameof(Register),
                    new { eventId });
            }

            _context.WaitlistEntries.Add(new WaitlistEntry
            {
                EventId = eventId,
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return RedirectToAction(
                "Details",
                "Events",
                new { id = eventId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LeaveWaitlist(int eventId)
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var waitlistEntry = await _context.WaitlistEntries
                .FirstOrDefaultAsync(w =>
                    w.EventId == eventId &&
                    w.UserId == userId);

            if (waitlistEntry != null)
            {
                _context.WaitlistEntries.Remove(waitlistEntry);

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(
                "Details",
                "Events",
                new { id = eventId });
        }

        public async Task<IActionResult> MyRegistrations()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var registrations = await _context.Registrations
                .Include(r => r.Event)
                .ThenInclude(e => e!.Venue)
                .Include(r => r.Event)
                .ThenInclude(e => e!.Club)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RegisteredAt)
                .ToListAsync();

            return View(registrations);
        }

        public async Task<IActionResult> MyWaitlist()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var waitlistEntries = await _context.WaitlistEntries
                .Include(w => w.Event)
                .ThenInclude(e => e!.Venue)
                .Include(w => w.Event)
                .ThenInclude(e => e!.Club)
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.JoinedAt)
                .ToListAsync();

            return View(waitlistEntries);
        }

        private async Task PromoteWaitlistedUser(int eventId)
        {
            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (eventItem == null)
            {
                return;
            }

            if (eventItem.ApprovalStatus != "Approved" ||
                eventItem.EndDateTime <= DateTime.Now)
            {
                return;
            }

            var registrationCount = await _context.Registrations
                .CountAsync(r => r.EventId == eventId);

            if (registrationCount >= eventItem.MaxParticipants)
            {
                return;
            }

            var nextEntry = await _context.WaitlistEntries
                .Where(w => w.EventId == eventId)
                .OrderBy(w => w.JoinedAt)
                .FirstOrDefaultAsync();

            if (nextEntry == null)
            {
                return;
            }

            var alreadyRegistered = await _context.Registrations
                .AnyAsync(r =>
                    r.EventId == eventId &&
                    r.UserId == nextEntry.UserId);

            if (alreadyRegistered)
            {
                _context.WaitlistEntries.Remove(nextEntry);
                await _context.SaveChangesAsync();

                return;
            }

            _context.Registrations.Add(new Registration
            {
                EventId = eventId,
                UserId = nextEntry.UserId,
                RegisteredAt = DateTime.UtcNow
            });

            _context.WaitlistEntries.Remove(nextEntry);

            await _context.SaveChangesAsync();
        }
    }
}