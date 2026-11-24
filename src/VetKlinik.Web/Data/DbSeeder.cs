using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Domain;

namespace VetKlinik.Web.Data;

public class SeedOptions
{
    public string AdminEmail { get; set; } = "";
    public string AdminPassword { get; set; } = "";
    public string AdminFullName { get; set; } = "";
}

public static class DbSeeder
{
    /// <summary>Rolleri ve Admin hesabını yoksa oluşturur; tekrar çalıştırmak güvenlidir.</summary>
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                Ensure(await roleManager.CreateAsync(new IdentityRole(role)));
        }

        if (await userManager.FindByEmailAsync(options.AdminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = options.AdminEmail,
                Email = options.AdminEmail,
                EmailConfirmed = true,
                FullName = options.AdminFullName,
            };
            // Ödevde istenen "sau" parolası, parola politikasını geçemez.
            // Bu yüzden özet doğrudan hesaplanır; politika diğer tüm kullanıcılar için geçerlidir.
            admin.PasswordHash = userManager.PasswordHasher.HashPassword(admin, options.AdminPassword);
            Ensure(await userManager.CreateAsync(admin));
            Ensure(await userManager.AddToRoleAsync(admin, Roles.Admin));
        }
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
