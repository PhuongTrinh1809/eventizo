using Eventizo.Models;
using Microsoft.AspNetCore.Identity;

namespace Eventizo.Data
{
    public static class SeedData
    {
        /// <summary>
        /// Seed Roles cần thiết
        /// </summary>
        public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            string[] roles = new[]
            {
                SD.Role_Admin,
                SD.Role_Customer,
                SD.Role_AdminEvent,
                SD.Role_AdminProduct,
                SD.Role_AdminTicket,
                SD.Role_AdminUsers
            };

            foreach (var roleName in roles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    var result = await roleManager.CreateAsync(new IdentityRole
                    {
                        Name = roleName,
                        NormalizedName = roleName.ToUpper()
                    });

                    if (!result.Succeeded)
                    {
                        Console.WriteLine($"Failed to create role '{roleName}':");
                        foreach (var error in result.Errors)
                            Console.WriteLine($" - {error.Description}");
                    }
                }
            }
        }

        /// <summary>
        /// Seed 5 admin TMH1→TMH5, lưu vào database
        /// Không auto login, giữ nguyên logic đăng nhập hiện tại
        /// </summary>
        public static async Task SeedAdminUsersAsync(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var admins = new[]
            {
                new { Email="TMH1@gmail.com", Password="Admin@123", Role=SD.Role_AdminEvent, FullName="Admin TMH1", DisplayName="TMH1" },
                new { Email="TMH2@gmail.com", Password="Admin@123", Role=SD.Role_AdminProduct, FullName="Admin TMH2", DisplayName="TMH2" },
                new { Email="TMH3@gmail.com", Password="Admin@123", Role=SD.Role_AdminTicket, FullName="Admin TMH3", DisplayName="TMH3" },
                new { Email="TMH4@gmail.com", Password="Admin@123", Role=SD.Role_AdminUsers, FullName="Admin TMH4", DisplayName="TMH4" },
                new { Email="TMH5@gmail.com", Password="Admin@123", Role=SD.Role_Admin, FullName="Admin TMH5", DisplayName="TMH5" }
            };

            foreach (var admin in admins)
            {
                try
                {
                    var existingUser = await userManager.FindByEmailAsync(admin.Email);
                    if (existingUser != null)
                    {
                        // User đã tồn tại, skip
                        continue;
                    }

                    var newUser = new ApplicationUser
                    {
                        UserName = admin.Email,
                        Email = admin.Email,
                        EmailConfirmed = true,
                        FullName = admin.FullName,
                        DisplayName = admin.DisplayName,
                        IsActive = true,
                        CreatedDate = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow,
                    };

                    var createResult = await userManager.CreateAsync(newUser, admin.Password);
                    if (!createResult.Succeeded)
                    {
                        Console.WriteLine($"Failed to create user {admin.Email}:");
                        foreach (var err in createResult.Errors)
                            Console.WriteLine($" - {err.Description}");
                        continue;
                    }

                    var roleResult = await userManager.AddToRoleAsync(newUser, admin.Role);
                    if (!roleResult.Succeeded)
                    {
                        Console.WriteLine($"Failed to assign role {admin.Role} to {admin.Email}:");
                        foreach (var err in roleResult.Errors)
                            Console.WriteLine($" - {err.Description}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception seeding user {admin.Email}: {ex.Message}");
                    if (ex.InnerException != null)
                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
            }
        }
    }
}
