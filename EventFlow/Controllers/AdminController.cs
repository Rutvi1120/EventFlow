using EventFlow.Data;
using EventFlow.Models;
using EventFlow.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ConflictDetectionService _conflictDetectionService;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ConflictDetectionService conflictDetectionService)
        {
            _context = context;
            _userManager = userManager;
            _conflictDetectionService = conflictDetectionService;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TotalUsers = await _userManager.Users.CountAsync();

            ViewBag.PendingUsers = await _userManager.Users
                .CountAsync(u => !u.IsApproved);

            ViewBag.TotalEvents = await _context.Events.CountAsync();

            ViewBag.PendingEvents = await _context.Events
                .CountAsync(e => e.ApprovalStatus == "Pending");

            ViewBag.TotalClubs = await _context.Clubs.CountAsync();

            ViewBag.ApprovedClubs = await _context.Clubs
                .CountAsync(c => c.Status == "Approved");

            ViewBag.TotalVenues = await _context.Venues.CountAsync();

            ViewBag.TotalRegistrations = await _context.Registrations.CountAsync();

            return View();
        }

        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users
                .Include(u => u.RequestedClub)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return View(users);
        }

       
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(user.RequestedRole))
            {
                TempData["ErrorMessage"] =
                    $"{user.FullName} does not have a requested role.";

                return RedirectToAction(nameof(Users));
            }

            if (user.RequestedRole != "Faculty" &&
                user.RequestedRole != "ClubPresident" &&
                user.RequestedRole != "Student")
            {
                TempData["ErrorMessage"] =
                    $"Invalid requested role for {user.FullName}.";

                return RedirectToAction(nameof(Users));
            }

          
            user.IsApproved = true;

            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                TempData["ErrorMessage"] = string.Join(
                    " ",
                    updateResult.Errors.Select(e => e.Description));

                return RedirectToAction(nameof(Users));
            }

           
            if (!await _userManager.IsInRoleAsync(
                user,
                user.RequestedRole))
            {
                var roleResult = await _userManager.AddToRoleAsync(
                    user,
                    user.RequestedRole);

                if (!roleResult.Succeeded)
                {
                    TempData["ErrorMessage"] = string.Join(
                        " ",
                        roleResult.Errors.Select(e => e.Description));

                    return RedirectToAction(nameof(Users));
                }
            }

            TempData["SuccessMessage"] =
                $"{user.FullName} has been approved as {user.RequestedRole}.";

            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            user.IsApproved = false;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = string.Join(
                    " ",
                    result.Errors.Select(e => e.Description));
            }
            else
            {
                TempData["SuccessMessage"] =
                    $"{user.FullName} has been rejected.";
            }

            return RedirectToAction(nameof(Users));
        }

        public async Task<IActionResult> Events()
        {
            var events = await _context.Events
                .Include(e => e.Organizer)
                .Include(e => e.Club)
                .Include(e => e.Venue)
                .Include(e => e.Registrations)
                .OrderByDescending(e => e.StartDateTime)
                .ToListAsync();

            return View(events);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveEvent(int id)
        {
            var eventItem = await _context.Events
                .Include(e => e.Venue)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (eventItem == null)
            {
                return NotFound();
            }

            
            if (eventItem.VenueId > 0)
            {
                var venueConflict =
                    await _conflictDetectionService.FindVenueConflictAsync(
                        eventItem.VenueId,
                        eventItem.StartDateTime,
                        eventItem.EndDateTime,
                        eventItem.Id);

                if (venueConflict != null)
                {
                    TempData["ErrorMessage"] =
                        $"Event cannot be approved because the selected venue " +
                        $"is already occupied by '{venueConflict.Name}' " +
                        $"from {venueConflict.StartDateTime:g} " +
                        $"to {venueConflict.EndDateTime:g}.";

                    return RedirectToAction(nameof(Events));
                }
            }

            eventItem.ApprovalStatus = "Approved";
            eventItem.Status = "Upcoming";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Event approved successfully.";

            return RedirectToAction(nameof(Events));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectEvent(int id)
        {
            var eventItem = await _context.Events.FindAsync(id);

            if (eventItem == null)
            {
                return NotFound();
            }

            eventItem.ApprovalStatus = "Rejected";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Event rejected.";

            return RedirectToAction(nameof(Events));
        }

        public async Task<IActionResult> Clubs()
        {
            var clubs = await _context.Clubs
                .Include(c => c.ClubPresident)
                .Include(c => c.FacultySupervisor)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(clubs);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveClub(int id)
        {
            var club = await _context.Clubs.FindAsync(id);

            if (club == null)
            {
                return NotFound();
            }

            club.Status = "Approved";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Club '{club.Name}' approved.";

            return RedirectToAction(nameof(Clubs));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectClub(int id)
        {
            var club = await _context.Clubs.FindAsync(id);

            if (club == null)
            {
                return NotFound();
            }

            club.Status = "Rejected";

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Club '{club.Name}' rejected.";

            return RedirectToAction(nameof(Clubs));
        }
    }
}