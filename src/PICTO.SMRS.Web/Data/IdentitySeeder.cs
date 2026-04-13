using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Security;

namespace PICTO.SMRS.Web.Data;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await db.Database.MigrateAsync();

        foreach (var role in SmrsRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                {
                    logger.LogError("Failed to create role {Role}: {Errors}", role,
                        string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }
        }

        var section = configuration.GetSection("Seed:InitialDepartmentHead");
        var userName = section["UserName"];
        var email = section["Email"];
        var password = section["Password"];

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Seed:InitialDepartmentHead is not configured (UserName, Email, Password). Skipping bootstrap admin user.");
            return;
        }

        var existing = await userManager.FindByNameAsync(userName);
        if (existing is not null)
            return;

        var user = new IdentityUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            logger.LogError("Failed to create seed user: {Errors}",
                string.Join("; ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        var roleResult = await userManager.AddToRoleAsync(user, SmrsRoles.DepartmentHead);
        if (!roleResult.Succeeded)
        {
            logger.LogError("Failed to assign DepartmentHead to seed user: {Errors}",
                string.Join("; ", roleResult.Errors.Select(e => e.Description)));
        }
    }
}
