using EventFlow.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EventFlow.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard()
        {
            var users = _userManager.Users.ToList();

            int totalUsers = users.Count;
            int totalAdmins = 0;
            int totalOrganizers = 0;
            int totalParticipants = 0;
            int totalVolunteers = 0;

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);

                if (roles.Contains("Admin"))
                    totalAdmins++;

                if (roles.Contains("Organizer"))
                    totalOrganizers++;

                if (roles.Contains("Participant"))
                    totalParticipants++;

                if (roles.Contains("Volunteer"))
                    totalVolunteers++;
            }

            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalAdmins = totalAdmins;
            ViewBag.TotalOrganizers = totalOrganizers;
            ViewBag.TotalParticipants = totalParticipants;
            ViewBag.TotalVolunteers = totalVolunteers;

            return View();
        }
    }
}