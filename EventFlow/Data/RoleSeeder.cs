
using EventFlow.Models;
using Microsoft.AspNetCore.Identity;

namespace EventFlow.Data
{
    public class RoleSeeder
    {
        public static async Task SeedRolesAsync(
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager)
        {
            string[] roles =
            {
                "Admin",
                "Organizer",
                "Participant",
                "Volunteer"
            };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(
                        new IdentityRole(role));
                }
            }

            string adminEmail = "admin@gmail.com";
            string adminPassword = "Admin12@123";

            var admin = await userManager.FindByEmailAsync(adminEmail);

            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FullName = "EventFlow Admin"
                };

                var result = await userManager.CreateAsync(
                    admin,
                    adminPassword);

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(
                        admin,
                        "Admin");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine(
                            $"ADMIN CREATE ERROR: {error.Code} - {error.Description}");
                    }
                }
            }
            else
            {
                var token =
                    await userManager.GeneratePasswordResetTokenAsync(admin);

                var result =
                    await userManager.ResetPasswordAsync(
                        admin,
                        token,
                        adminPassword);

                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine(
                            $"ADMIN PASSWORD RESET ERROR: {error.Code} - {error.Description}");
                    }
                }

                if (!await userManager.IsInRoleAsync(admin, "Admin"))
                {
                    await userManager.AddToRoleAsync(
                        admin,
                        "Admin");
                }
            }
        }
    }
}
