using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VetKlinik.Tests.Infrastructure;
using VetKlinik.Web.Data;
using VetKlinik.Web.Domain;
using VetKlinik.Web.Models.Admin;
using VetKlinik.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace VetKlinik.Tests.Services;

public class VeterinarianServiceTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private static readonly DateTime Monday9 = new(2026, 12, 7, 9, 0, 0);

    private (ServiceProvider Sp, IServiceScope Scope, VeterinarianService Service, AppDbContext Db) Create()
    {
        var sp = TestServices.Build(fixture.ConnectionString);
        var scope = sp.CreateScope();
        return (sp, scope, ActivatorUtilities.CreateInstance<VeterinarianService>(scope.ServiceProvider),
            scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static async Task<VeterinarianFormModel> FormAsync(VeterinarianService service, Seeded seed, string email)
    {
        var form = await service.NewFormAsync();
        form.FullName = "Dr. Zeynep Kaya";
        form.Email = email;
        form.Password = "hekim123";
        form.Title = "Vet. Hek.";
        form.IsActive = true;
        form.ServiceIds = [seed.Examination.Id];
        form.SpecialtyIds = [];
        form.Days[0].Works = true; // Pazartesi
        form.Days[0].Starts = new TimeOnly(9, 0);
        form.Days[0].Ends = new TimeOnly(13, 0);
        return form;
    }

    [Fact]
    public async Task Create_makes_user_in_Veteriner_role_with_services_and_hours()
    {
        var seed = await fixture.ResetAsync();
        var (sp, scope, service, _) = Create();
        using var _s = scope; await using var _p = sp;

        var result = await service.CreateAsync(await FormAsync(service, seed, "zeynep@test.local"));

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync("zeynep@test.local");
        Assert.NotNull(user);
        Assert.True(await users.IsInRoleAsync(user, "Veteriner"));
        Assert.True(await users.CheckPasswordAsync(user, "hekim123"));
        await using var db = fixture.NewContext();
        var vet = await db.Veterinarians.Include(v => v.Services).Include(v => v.Availabilities)
            .SingleAsync(v => v.UserId == user.Id);
        Assert.Equal([seed.Examination.Id], vet.Services.Select(s => s.Id));
        var monday = Assert.Single(vet.Availabilities);
        Assert.Equal(DayOfWeek.Monday, monday.Day);
        Assert.Equal(new TimeOnly(13, 0), monday.Ends);
    }

    [Fact]
    public async Task Create_with_taken_email_fails_and_leaves_nothing()
    {
        var seed = await fixture.ResetAsync();
        var (sp, scope, service, _) = Create();
        using var _s = scope; await using var _p = sp;
        await using var before = fixture.NewContext();
        var vetCount = await before.Veterinarians.CountAsync();
        var userCount = await before.Users.CountAsync();

        var result = await service.CreateAsync(await FormAsync(service, seed, "ownera@test.local"));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("zaten"));
        await using var after = fixture.NewContext();
        Assert.Equal(vetCount, await after.Veterinarians.CountAsync());
        Assert.Equal(userCount, await after.Users.CountAsync());
    }

    [Fact]
    public async Task Create_requires_password()
    {
        var seed = await fixture.ResetAsync();
        var (sp, scope, service, _) = Create();
        using var _s = scope; await using var _p = sp;
        var form = await FormAsync(service, seed, "parolasiz@test.local");
        form.Password = null;

        var result = await service.CreateAsync(form);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("Parola"));
    }

    [Fact]
    public async Task Availability_outside_clinic_hours_is_rejected()
    {
        var seed = await fixture.ResetAsync();
        var (sp, scope, service, _) = Create();
        using var _s = scope; await using var _p = sp;
        var form = await FormAsync(service, seed, "erken@test.local");
        form.Days[0].Starts = new TimeOnly(8, 0);

        var result = await service.CreateAsync(form);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.StartsWith("Pazartesi"));
    }

    [Fact]
    public async Task Availability_on_closed_day_is_rejected()
    {
        var seed = await fixture.ResetAsync();
        var (sp, scope, service, _) = Create();
        using var _s = scope; await using var _p = sp;
        var form = await FormAsync(service, seed, "pazar@test.local");
        form.Days[6].Works = true;
        form.Days[6].Starts = new TimeOnly(10, 0);
        form.Days[6].Ends = new TimeOnly(12, 0);

        var result = await service.CreateAsync(form);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.StartsWith("Pazar"));
    }

    [Fact]
    public async Task Unknown_specialty_and_service_ids_are_ignored()
    {
        var seed = await fixture.ResetAsync();
        var (sp, scope, service, _) = Create();
        using var _s = scope; await using var _p = sp;
        var form = await FormAsync(service, seed, "bilinmeyen@test.local");
        form.ServiceIds = [seed.Examination.Id, 999999];
        form.SpecialtyIds = [999999];

        var result = await service.CreateAsync(form);

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        await using var db = fixture.NewContext();
        var vet = await db.Veterinarians.Include(v => v.Services).Include(v => v.Specialties)
            .SingleAsync(v => v.Id == result.Value!.Id);
        Assert.Single(vet.Services);
        Assert.Empty(vet.Specialties);
    }

    [Fact]
    public async Task Update_replaces_services_specialties_and_hours()
    {
        var seed = await fixture.ResetAsync();
        var (sp, scope, service, _) = Create();
        using var _s = scope; await using var _p = sp;
        var form = (await service.GetFormAsync(seed.VetA.Id))!;
        form.FullName = "Hekim A Yeni";
        form.ServiceIds = [seed.Neutering.Id];
        form.SpecialtyIds = [];
        foreach (var day in form.Days) day.Works = false;
        form.Days[2].Works = true; // Çarşamba
        form.Days[2].Starts = new TimeOnly(12, 0);
        form.Days[2].Ends = new TimeOnly(17, 0);

        var result = await service.UpdateAsync(seed.VetA.Id, form);

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        await using var db = fixture.NewContext();
        var vet = await db.Veterinarians.Include(v => v.User).Include(v => v.Services)
            .Include(v => v.Specialties).Include(v => v.Availabilities).SingleAsync(v => v.Id == seed.VetA.Id);
        Assert.Equal("Hekim A Yeni", vet.User.FullName);
        Assert.Equal([seed.Neutering.Id], vet.Services.Select(s => s.Id));
        Assert.Empty(vet.Specialties);
        Assert.Equal(DayOfWeek.Wednesday, Assert.Single(vet.Availabilities).Day);
    }

    [Fact]
    public async Task Delete_with_appointment_is_refused()
    {
        var seed = await fixture.ResetAsync();
        await using (var setup = fixture.NewContext())
        {
            setup.Appointments.Add(seed.NewAppointment(Monday9));
            await setup.SaveChangesAsync();
        }
        var (sp, scope, service, _) = Create();
        using var _s = scope; await using var _p = sp;

        var result = await service.DeleteAsync(seed.VetA.Id);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("pasif"));
    }

    [Fact]
    public async Task Delete_without_appointment_removes_vet_and_user()
    {
        var seed = await fixture.ResetAsync();
        var (sp, scope, service, _) = Create();
        using var _s = scope; await using var _p = sp;

        var result = await service.DeleteAsync(seed.VetB.Id);

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        await using var db = fixture.NewContext();
        Assert.False(await db.Veterinarians.AnyAsync(v => v.Id == seed.VetB.Id));
        Assert.False(await db.Users.AnyAsync(u => u.Email == "vetb@test.local"));
    }
}
