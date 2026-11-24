using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedClinicAsync(db);
        await SeedServicesAsync(db);
    }

    private static async Task SeedClinicAsync(AppDbContext db)
    {
        if (await db.Clinics.AnyAsync())
            return;

        db.Clinics.Add(new Clinic
        {
            Name = "VetKlinik Sakarya",
            Address = "Serdivan, Sakarya",
            Phone = "0264 000 00 00",
            Hours = [.. Enum.GetValues<DayOfWeek>().Select(day => day switch
            {
                DayOfWeek.Sunday => new ClinicHours { Day = day, IsClosed = true },
                DayOfWeek.Saturday => new ClinicHours { Day = day, Opens = new(9, 0), Closes = new(14, 0) },
                _ => new ClinicHours { Day = day, Opens = new(9, 0), Closes = new(18, 0) },
            })],
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedServicesAsync(AppDbContext db)
    {
        if (await db.Services.AnyAsync())
            return;

        db.Services.AddRange(
            new Service { Name = "Muayene", Description = "Genel sağlık kontrolü.", DurationMinutes = 30, Price = 500m },
            new Service { Name = "Aşı", Description = "Karma, kuduz ve diğer aşılar.", DurationMinutes = 15, Price = 350m },
            new Service { Name = "Kısırlaştırma", Description = "Genel anestezi altında operasyon.", DurationMinutes = 90, Price = 3000m },
            new Service { Name = "Diş Temizliği", Description = "Diş taşı temizliği ve kontrol.", DurationMinutes = 60, Price = 1500m },
            new Service { Name = "Tıraş ve Bakım", Description = "Tüy kesimi, banyo ve tırnak bakımı.", DurationMinutes = 45, Price = 600m });
        await db.SaveChangesAsync();
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
