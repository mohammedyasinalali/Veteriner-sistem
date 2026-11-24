using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetKlinik.Tests.Infrastructure;
using VetKlinik.Web.Data;
using VetKlinik.Web.Domain;

namespace VetKlinik.Tests.Services;

public class DbSeederTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private const string AdminEmail = "g231210566@sakarya.edu.tr";

    [Fact]
    public async Task Creates_three_roles()
    {
        await fixture.ClearAsync();
        await using var sp = TestServices.Build(fixture.ConnectionString);

        await DbSeeder.SeedAsync(sp);

        using var scope = sp.CreateScope();
        var roles = await scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>()
            .Roles.Select(r => r.Name).ToListAsync();
        Assert.Equivalent(new[] { "Admin", "Veteriner", "Uye" }, roles);
    }

    [Fact]
    public async Task Creates_admin_with_sau_password()
    {
        await fixture.ClearAsync();
        await using var sp = TestServices.Build(fixture.ConnectionString);

        await DbSeeder.SeedAsync(sp);

        using var scope = sp.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await users.FindByEmailAsync(AdminEmail);
        Assert.NotNull(admin);
        Assert.True(await users.CheckPasswordAsync(admin, "sau"));
        Assert.True(await users.IsInRoleAsync(admin, "Admin"));
    }

    [Fact]
    public async Task Running_twice_does_not_duplicate()
    {
        await fixture.ClearAsync();
        await using var sp = TestServices.Build(fixture.ConnectionString);

        await DbSeeder.SeedAsync(sp);
        await DbSeeder.SeedAsync(sp);

        await using var db = fixture.NewContext();
        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == AdminEmail));
        Assert.Equal(3, await db.Roles.CountAsync());
    }

    [Fact]
    public async Task Seeds_clinic_and_services_once()
    {
        await fixture.ClearAsync();
        await using var sp = TestServices.Build(fixture.ConnectionString);

        await DbSeeder.SeedAsync(sp);
        await DbSeeder.SeedAsync(sp);

        await using var db = fixture.NewContext();
        var clinic = await db.Clinics.Include(c => c.Hours).SingleAsync();
        Assert.Equal(7, clinic.Hours.Count);
        Assert.True(clinic.Hours.Single(h => h.Day == DayOfWeek.Sunday).IsClosed);
        Assert.Equal(5, await db.Services.CountAsync());
    }
}
