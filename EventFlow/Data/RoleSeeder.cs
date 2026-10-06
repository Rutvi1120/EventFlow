using EventFlow.Models;
using Microsoft.AspNetCore.Identity;

namespace EventFlow.Data
{
    public static class RoleSeeder
    {
        private static readonly string[] Roles =
        {
            "Admin",
            "Student",
            "Faculty",
            "ClubPresident"
        };

        private const string AdminEmail = "admin@gmail.com";
        private const string AdminPassword = "Admin@123";

        public static async Task SeedAsync(
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            var roleManager =
                serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            var userManager =
                serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        
            foreach (var role in Roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var result = await roleManager.CreateAsync(
                        new IdentityRole(role));

                    if (!result.Succeeded)
                    {
                        throw new InvalidOperationException(
                            $"Failed to create role '{role}': " +
                            string.Join(
                                ", ",
                                result.Errors.Select(
                                    e => e.Description)));
                    }
                }
            }

          

            var admin =
                await userManager.FindByEmailAsync(AdminEmail);

           

            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = AdminEmail,
                    Email = AdminEmail,
                    EmailConfirmed = true,
                    FullName = "System Administrator",
                    IsApproved = true,
                    RequestedRole = "Admin"
                };

                var createResult =
                    await userManager.CreateAsync(
                        admin,
                        AdminPassword);

                if (!createResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Failed to create administrator account: " +
                        string.Join(
                            ", ",
                            createResult.Errors.Select(
                                e => e.Description)));
                }
            }


            if (!admin.IsApproved)
            {
                admin.IsApproved = true;
            }

            admin.EmailConfirmed = true;
            admin.RequestedRole = "Admin";

            var updateResult =
                await userManager.UpdateAsync(admin);

            if (!updateResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Failed to update administrator account: " +
                    string.Join(
                        ", ",
                        updateResult.Errors.Select(
                            e => e.Description)));
            }

          

            if (!await userManager.IsInRoleAsync(
                admin,
                "Admin"))
            {
                var roleResult =
                    await userManager.AddToRoleAsync(
                        admin,
                        "Admin");

                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Failed to assign Admin role: " +
                        string.Join(
                            ", ",
                            roleResult.Errors.Select(
                                e => e.Description)));
                }
            }
        }
    }
}