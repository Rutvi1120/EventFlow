using EventFlow.Data;
using EventFlow.Models;
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

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
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

            user.IsApproved = true;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                return View(
                    "Users",
                    await _userManager.Users
                        .Include(u => u.RequestedClub)
                        .OrderBy(u => u.FullName)
                        .ToListAsync());
            }

            if (!string.IsNullOrWhiteSpace(user.RequestedRole))
            {
                var validRoles = new[]
                {
                    "Faculty",
                    "ClubPresident"
                };

                if (validRoles.Contains(user.RequestedRole) &&
                    !await _userManager.IsInRoleAsync(
                        user,
                        user.RequestedRole))
                {
                    var roleResult = await _userManager.AddToRoleAsync(
                        user,
                        user.RequestedRole);

                    if (!roleResult.Succeeded)
                    {
                        foreach (var error in roleResult.Errors)
                        {
                            ModelState.AddModelError(
                                string.Empty,
                                error.Description);
                        }
                    }
                }
            }

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
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }
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
            var eventItem = await _context.Events.FindAsync(id);

            if (eventItem == null)
            {
                return NotFound();
            }

            eventItem.ApprovalStatus = "Approved";

            await _context.SaveChangesAsync();

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

            return RedirectToAction(nameof(Clubs));
        }
    }
}