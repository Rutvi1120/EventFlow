using System.Data;
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

        // =====================================================
        // REGISTER
        // If the event is full the user is placed on the waitlist.
        // =====================================================

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
                .FirstOrDefaultAsync(e => e.Id == eventId);

            var problem = GetSignupProblem(eventItem);

            if (problem != null)
            {
                TempData["ErrorMessage"] = problem;

                return RedirectToAction("Index", "Events");
            }

            if (await _context.Registrations.AnyAsync(r =>
                    r.EventId == eventId && r.UserId == userId))
            {
                TempData["InfoMessage"] =
                    "You are already registered for this event.";

                return RedirectToDetails(eventId);
            }

            if (await _context.WaitlistEntries.AnyAsync(w =>
                    w.EventId == eventId && w.UserId == userId))
            {
                TempData["InfoMessage"] =
                    "You are already on the waitlist for this event.";

                return RedirectToDetails(eventId);
            }

            try
            {
                // Serializable so two students cannot both take the last seat.
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                var registrationCount = await _context.Registrations
                    .CountAsync(r => r.EventId == eventId);

                if (registrationCount >= eventItem!.MaxParticipants)
                {
                    _context.WaitlistEntries.Add(new WaitlistEntry
                    {
                        EventId = eventId,
                        UserId = userId,
                        JoinedAt = DateTime.UtcNow
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["InfoMessage"] =
                        "This event is full. You have been added to the waitlist " +
                        "and will be registered automatically if a seat opens up.";
                }
                else
                {
                    _context.Registrations.Add(new Registration
                    {
                        EventId = eventId,
                        UserId = userId,
                        RegisteredAt = DateTime.UtcNow
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] =
                        "You have been registered for this event.";
                }
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] =
                    "Your registration could not be completed. " +
                    "You may already be registered - please check My Registrations.";
            }

            return RedirectToDetails(eventId);
        }

        // =====================================================
        // CANCEL REGISTRATION
        // =====================================================

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
                .Include(r => r.Event)
                .FirstOrDefaultAsync(r =>
                    r.EventId == eventId &&
                    r.UserId == userId);

            if (registration == null)
            {
                TempData["InfoMessage"] =
                    "You are not registered for this event.";

                return RedirectToDetails(eventId);
            }

            if (registration.Event != null && registration.Event.HasEnded)
            {
                TempData["ErrorMessage"] =
                    "This event has already ended, so the registration cannot be cancelled.";

                return RedirectToAction(nameof(MyRegistrations));
            }

            _context.Registrations.Remove(registration);

            await _context.SaveChangesAsync();

            await PromoteWaitlistedUser(eventId);

            TempData["SuccessMessage"] = "Your registration has been cancelled.";

            return RedirectToDetails(eventId);
        }

        // =====================================================
        // JOIN / LEAVE WAITLIST
        // =====================================================

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
                .FirstOrDefaultAsync(e => e.Id == eventId);

            var problem = GetSignupProblem(eventItem);

            if (problem != null)
            {
                TempData["ErrorMessage"] = problem;

                return RedirectToAction("Index", "Events");
            }

            if (await _context.Registrations.AnyAsync(r =>
                    r.EventId == eventId && r.UserId == userId))
            {
                TempData["InfoMessage"] =
                    "You are already registered for this event.";

                return RedirectToDetails(eventId);
            }

            if (await _context.WaitlistEntries.AnyAsync(w =>
                    w.EventId == eventId && w.UserId == userId))
            {
                TempData["InfoMessage"] =
                    "You are already on the waitlist for this event.";

                return RedirectToDetails(eventId);
            }

            var registrationCount = await _context.Registrations
                .CountAsync(r => r.EventId == eventId);

            if (registrationCount < eventItem!.MaxParticipants)
            {
                TempData["InfoMessage"] =
                    "Seats are still available - you can register directly.";

                return RedirectToDetails(eventId);
            }

            _context.WaitlistEntries.Add(new WaitlistEntry
            {
                EventId = eventId,
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            });

            try
            {
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    "You have been added to the waitlist.";
            }
            catch (DbUpdateException)
            {
                TempData["InfoMessage"] =
                    "You are already on the waitlist for this event.";
            }

            return RedirectToDetails(eventId);
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

                TempData["SuccessMessage"] =
                    "You have left the waitlist.";
            }

            return RedirectToDetails(eventId);
        }

        // =====================================================
        // MY REGISTRATIONS / MY WAITLIST
        // =====================================================

        public async Task<IActionResult> MyRegistrations()
        {
            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var registrations = await _context.Registrations
                .AsNoTracking()
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
                .AsNoTracking()
                .Include(w => w.Event)
                .ThenInclude(e => e!.Venue)
                .Include(w => w.Event)
                .ThenInclude(e => e!.Club)
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.JoinedAt)
                .ToListAsync();

            return View(waitlistEntries);
        }

        // =====================================================
        // HELPERS
        // =====================================================

        private IActionResult RedirectToDetails(int eventId)
        {
            return RedirectToAction(
                "Details",
                "Events",
                new { id = eventId });
        }

        /// <summary>
        /// Registration / waitlist is only possible for approved events
        /// that have not started yet. Returns an error message, or null if open.
        /// </summary>
        private static string? GetSignupProblem(Event? eventItem)
        {
            if (eventItem == null)
            {
                return "The selected event could not be found.";
            }

            if (eventItem.ApprovalStatus != EventApprovalStatus.Approved)
            {
                return "Registration is only available for approved events.";
            }

            if (eventItem.HasEnded)
            {
                return "This event has already ended.";
            }

            if (eventItem.HasStarted)
            {
                return "Registration is closed because the event has already started.";
            }

            return null;
        }

        private async Task PromoteWaitlistedUser(int eventId)
        {
            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (eventItem == null ||
                eventItem.ApprovalStatus != EventApprovalStatus.Approved ||
                eventItem.HasStarted)
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
