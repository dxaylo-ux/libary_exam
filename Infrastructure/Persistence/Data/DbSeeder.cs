using Application.Identity;
using Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Persistence.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        foreach (var role in Enum.GetNames<UserRole>())
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<int>(role));
        }

        await EnsureUserAsync(userManager, "admin@mail.com", "Admin123", "Admin", "Admin", UserRole.Admin);
        await EnsureUserAsync(userManager, "librarian@mail.com", "Librarian123", "Librarian", "Librarian", UserRole.Librarian);
    }

    private static async Task EnsureUserAsync(
        UserManager<User> userManager, string email, string password,
        string firstName, string lastName, UserRole role)
    {
        if (await userManager.FindByEmailAsync(email) != null)
            return;

        var user = new User
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            BirthDate = new DateOnly(1990, 1, 1)
        };

        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(user, role.ToString());
    }
}
