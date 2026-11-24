using Microsoft.EntityFrameworkCore;
using VetKlinik.Tests.Infrastructure;
using VetKlinik.Web.Models.Admin;
using VetKlinik.Web.Services;

namespace VetKlinik.Tests.Services;

public class ServiceCatalogServiceTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private static readonly DateTime Monday9 = new(2026, 12, 7, 9, 0, 0);

    private static ServiceFormModel Form(string name, bool active = true) => new()
    {
        Name = name,
        Description = "Açıklama",
        DurationMinutes = 45,
        Price = 750m,
        IsActive = active,
    };

    [Fact]
    public async Task Create_adds_service()
    {
        await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var result = await new ServiceCatalogService(db).CreateAsync(Form("Tıraş ve Bakım"));

        Assert.True(result.Succeeded);
        await using var fresh = fixture.NewContext();
        var saved = await fresh.Services.SingleAsync(s => s.Name == "Tıraş ve Bakım");
        Assert.Equal(45, saved.DurationMinutes);
        Assert.Equal(750m, saved.Price);
    }

    [Fact]
    public async Task Create_rejects_duplicate_name_case_insensitive()
    {
        await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var result = await new ServiceCatalogService(db).CreateAsync(Form("muayene"));

        Assert.False(result.Succeeded);
        Assert.Equal(1, await db.Services.CountAsync(s => s.Name == "Muayene"));
    }

    [Fact]
    public async Task Update_unknown_id_fails()
    {
        await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var result = await new ServiceCatalogService(db).UpdateAsync(999999, Form("Yok"));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Update_changes_fields()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var result = await new ServiceCatalogService(db).UpdateAsync(seed.Examination.Id, Form("Genel Muayene", active: false));

        Assert.True(result.Succeeded);
        await using var fresh = fixture.NewContext();
        var saved = await fresh.Services.SingleAsync(s => s.Id == seed.Examination.Id);
        Assert.Equal("Genel Muayene", saved.Name);
        Assert.False(saved.IsActive);
    }

    [Fact]
    public async Task Delete_with_appointment_is_refused_with_message()
    {
        var seed = await fixture.ResetAsync();
        await using (var setup = fixture.NewContext())
        {
            setup.Appointments.Add(seed.NewAppointment(Monday9));
            await setup.SaveChangesAsync();
        }
        await using var db = fixture.NewContext();

        var result = await new ServiceCatalogService(db).DeleteAsync(seed.Examination.Id);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("pasif"));
        Assert.True(await db.Services.AnyAsync(s => s.Id == seed.Examination.Id));
    }

    [Fact]
    public async Task Delete_without_appointment_removes()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var result = await new ServiceCatalogService(db).DeleteAsync(seed.Neutering.Id);

        Assert.True(result.Succeeded);
        await using var fresh = fixture.NewContext();
        Assert.False(await fresh.Services.AnyAsync(s => s.Id == seed.Neutering.Id));
    }

    [Fact]
    public async Task List_without_inactive_hides_passive()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();
        var catalog = new ServiceCatalogService(db);
        await catalog.UpdateAsync(seed.Neutering.Id, Form("Kısırlaştırma", active: false));

        var publicList = await catalog.ListAsync(includeInactive: false);
        var adminList = await catalog.ListAsync(includeInactive: true);

        Assert.DoesNotContain(publicList, s => s.Name == "Kısırlaştırma");
        Assert.Contains(adminList, s => s.Name == "Kısırlaştırma");
    }
}
