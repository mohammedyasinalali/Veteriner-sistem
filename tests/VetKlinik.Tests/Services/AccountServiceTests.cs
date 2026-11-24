using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetKlinik.Tests.Infrastructure;
using VetKlinik.Web.Data;
using VetKlinik.Web.Domain;
using VetKlinik.Web.Models.Account;
using VetKlinik.Web.Services;

namespace VetKlinik.Tests.Services;

public class AccountServiceTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private static RegisterViewModel NewMember(string email) => new()
    {
        FullName = "Ayşe Yılmaz",
        Email = email,
        Password = "pamuk1",
        ConfirmPassword = "pamuk1",
    };

    [Fact]
    public async Task Register_creates_member_in_Uye_role()
    {
        await fixture.ClearAsync();
        await using var sp = TestServices.Build(fixture.ConnectionString);
        await DbSeeder.SeedAsync(sp);
        using var scope = sp.CreateScope();
        var service = ActivatorUtilities.CreateInstance<AccountService>(scope.ServiceProvider);

        var result = await service.RegisterAsync(NewMember("ayse@test.local"));

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync("ayse@test.local");
        Assert.NotNull(user);
        Assert.Equal(["Uye"], await users.GetRolesAsync(user));
    }

    [Fact]
    public async Task Register_rejects_duplicate_email()
    {
        await fixture.ClearAsync();
        await using var sp = TestServices.Build(fixture.ConnectionString);
        await DbSeeder.SeedAsync(sp);
        using var scope = sp.CreateScope();
        var service = ActivatorUtilities.CreateInstance<AccountService>(scope.ServiceProvider);
        await service.RegisterAsync(NewMember("ayse@test.local"));

        var second = await service.RegisterAsync(NewMember("ayse@test.local"));

        Assert.False(second.Succeeded);
        Assert.Single(second.Errors, e => e.Contains("zaten"));
        await using var db = fixture.NewContext();
        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == "ayse@test.local"));
    }

    [Fact]
    public async Task Register_with_turkish_letters_in_email_gives_turkish_error()
    {
        await fixture.ClearAsync();
        await using var sp = TestServices.Build(fixture.ConnectionString);
        await DbSeeder.SeedAsync(sp);
        using var scope = sp.CreateScope();
        var service = ActivatorUtilities.CreateInstance<AccountService>(scope.ServiceProvider);

        var result = await service.RegisterAsync(NewMember("ayşe@test.local"));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("yalnızca"));
    }
}
