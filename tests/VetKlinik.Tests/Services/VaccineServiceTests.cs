using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using VetKlinik.Tests.Infrastructure;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Data;
using VetKlinik.Web.Domain;
using VetKlinik.Web.Models.Pets;
using VetKlinik.Web.Services;

namespace VetKlinik.Tests.Services;

public class VaccineServiceTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    // "Bugün" sabit: 1 Aralık 2026 (Istanbul, UTC+3).
    private static readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.FromHours(3)));

    private static VaccineService Service(AppDbContext db) =>
        new(db, new PetService(db, new PhotoStorage(Path.GetTempPath()), Clock), Clock);

    private static CurrentUser Member(ApplicationUser u) => new(u.Id, false, false);
    private static CurrentUser Admin(ApplicationUser u) => new(u.Id, true, false);
    private static CurrentUser Vet(Veterinarian v) => new(v.UserId, false, true);

    private static VaccineFormModel Form(string name = "Kuduz", string on = "2026-11-20", string? next = "2027-11-20") => new()
    {
        VaccineName = name,
        AdministeredOn = DateOnly.Parse(on),
        NextDueOn = next is null ? null : DateOnly.Parse(next),
    };

    private async Task AddAppointmentAsync(Seeded seed)
    {
        await using var db = fixture.NewContext();
        db.Appointments.Add(seed.NewAppointment(new DateTime(2026, 12, 7, 10, 0, 0))); // PetA ↔ VetA
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Owner_adds_vaccine()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var result = await Service(db).AddAsync(Member(seed.OwnerA), seed.PetA.Id, Form());

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        await using var fresh = fixture.NewContext();
        var record = await fresh.VaccineRecords.SingleAsync();
        Assert.Equal(seed.PetA.Id, record.PetId);
        Assert.Null(record.VeterinarianId);
        Assert.Equal(new DateOnly(2027, 11, 20), record.NextDueOn);
    }

    [Fact]
    public async Task Other_member_cannot_add_view_or_edit()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();
        var service = Service(db);
        var own = await service.AddAsync(Member(seed.OwnerA), seed.PetA.Id, Form());

        Assert.False((await service.AddAsync(Member(seed.OwnerB), seed.PetA.Id, Form("Karma"))).Succeeded);
        Assert.Null(await service.GetScheduleAsync(Member(seed.OwnerB), seed.PetA.Id));
        Assert.Null(await service.FindEditableAsync(Member(seed.OwnerB), own.Value!.Id));
        Assert.False((await service.UpdateAsync(Member(seed.OwnerB), own.Value.Id, Form("Değişti"))).Succeeded);
        Assert.False((await service.DeleteAsync(Member(seed.OwnerB), own.Value.Id)).Succeeded);

        await using var fresh = fixture.NewContext();
        Assert.Equal("Kuduz", (await fresh.VaccineRecords.SingleAsync()).VaccineName);
    }

    [Fact]
    public async Task Vet_with_appointment_adds_vaccine_with_vet_id()
    {
        var seed = await fixture.ResetAsync();
        await AddAppointmentAsync(seed);
        await using var db = fixture.NewContext();

        var result = await Service(db).AddAsync(Vet(seed.VetA), seed.PetA.Id, Form());

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        await using var fresh = fixture.NewContext();
        Assert.Equal(seed.VetA.Id, (await fresh.VaccineRecords.SingleAsync()).VeterinarianId);
    }

    [Fact]
    public async Task Vet_without_appointment_cannot_add()
    {
        var seed = await fixture.ResetAsync();
        await AddAppointmentAsync(seed);
        await using var db = fixture.NewContext();

        var result = await Service(db).AddAsync(Vet(seed.VetB), seed.PetA.Id, Form());

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Owner_cannot_edit_vet_record_but_vet_and_admin_can()
    {
        var seed = await fixture.ResetAsync();
        await AddAppointmentAsync(seed);
        await using var db = fixture.NewContext();
        var service = Service(db);
        var record = (await service.AddAsync(Vet(seed.VetA), seed.PetA.Id, Form())).Value!;

        Assert.Null(await service.FindEditableAsync(Member(seed.OwnerA), record.Id));
        Assert.False((await service.DeleteAsync(Member(seed.OwnerA), record.Id)).Succeeded);
        Assert.NotNull(await service.FindEditableAsync(Vet(seed.VetA), record.Id));
        Assert.NotNull(await service.FindEditableAsync(Admin(seed.Admin), record.Id));
    }

    [Fact]
    public async Task Future_administered_date_is_rejected()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var result = await Service(db).AddAsync(Member(seed.OwnerA), seed.PetA.Id, Form(on: "2026-12-02", next: null));

        Assert.False(result.Succeeded);
        Assert.Contains("Uygulama tarihi", string.Join(" ", result.Errors));
    }

    [Fact]
    public async Task Next_due_must_be_after_administered()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var result = await Service(db).AddAsync(Member(seed.OwnerA), seed.PetA.Id, Form(on: "2026-11-20", next: "2026-11-20"));

        Assert.False(result.Succeeded);
        Assert.Contains("Sonraki doz", string.Join(" ", result.Errors));
    }

    [Fact]
    public async Task Schedule_uses_latest_dose_and_marks_overdue()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();
        var service = Service(db);
        var owner = Member(seed.OwnerA);
        await service.AddAsync(owner, seed.PetA.Id, Form("Kuduz", "2025-11-01", "2026-11-01"));   // yenilendi ↓
        await service.AddAsync(owner, seed.PetA.Id, Form("kuduz", "2026-11-05", "2027-11-05"));
        await service.AddAsync(owner, seed.PetA.Id, Form("Karma", "2025-10-01", "2026-11-25"));   // gecikmiş
        await service.AddAsync(owner, seed.PetA.Id, Form("İç Parazit", "2026-11-28", null));      // tekrar yok

        var schedule = (await service.GetScheduleAsync(owner, seed.PetA.Id))!;

        Assert.Equal(4, schedule.History.Count);
        Assert.Equal("İç Parazit", schedule.History[0].VaccineName); // yeniden eskiye
        Assert.Collection(schedule.Upcoming,
            karma =>
            {
                Assert.Equal("Karma", karma.VaccineName);
                Assert.True(karma.IsOverdue);
                Assert.Equal(-6, karma.DaysLeft);
            },
            kuduz =>
            {
                Assert.Equal(new DateOnly(2027, 11, 5), kuduz.DueOn);
                Assert.False(kuduz.IsOverdue);
            });
    }

    [Fact]
    public async Task Update_changes_fields_but_keeps_pet()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();
        var service = Service(db);
        var record = (await service.AddAsync(Member(seed.OwnerA), seed.PetA.Id, Form())).Value!;

        var result = await service.UpdateAsync(Member(seed.OwnerA), record.Id, Form("Kuduz (tekrar)", "2026-11-21", null));

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        await using var fresh = fixture.NewContext();
        var saved = await fresh.VaccineRecords.SingleAsync();
        Assert.Equal("Kuduz (tekrar)", saved.VaccineName);
        Assert.Null(saved.NextDueOn);
        Assert.Equal(seed.PetA.Id, saved.PetId);
    }

    [Fact]
    public async Task Owner_deletes_own_record()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();
        var service = Service(db);
        var record = (await service.AddAsync(Member(seed.OwnerA), seed.PetA.Id, Form())).Value!;

        Assert.True((await service.DeleteAsync(Member(seed.OwnerA), record.Id)).Succeeded);

        await using var fresh = fixture.NewContext();
        Assert.False(await fresh.VaccineRecords.AnyAsync());
    }
}
