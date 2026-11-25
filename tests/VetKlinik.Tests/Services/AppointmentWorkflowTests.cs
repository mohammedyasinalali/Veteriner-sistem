using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using VetKlinik.Tests.Infrastructure;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Data;
using VetKlinik.Web.Domain;
using VetKlinik.Web.Services;

namespace VetKlinik.Tests.Services;

public class AppointmentWorkflowTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private static readonly DateOnly Tuesday = new(2026, 12, 8);
    private static DateTime At(int hour, int minute = 0) => Tuesday.ToDateTime(new TimeOnly(hour, minute));

    /// <summary>Varsayılan "şimdi": Pazartesi 7 Aralık 08:00 (randevular Salı).</summary>
    private static FakeTimeProvider Clock(DateTime? local = null) =>
        new(new DateTimeOffset(local ?? new DateTime(2026, 12, 7, 8, 0, 0), TimeSpan.FromHours(3)));

    private static AppointmentService Service(AppDbContext db, FakeTimeProvider? clock = null)
    {
        clock ??= Clock();
        return new AppointmentService(db, new PetService(db, new PhotoStorage(Path.GetTempPath()), clock), clock);
    }

    private static CurrentUser Member(ApplicationUser u) => new(u.Id, false, false);
    private static CurrentUser Admin(ApplicationUser u) => new(u.Id, true, false);
    private static CurrentUser Vet(Veterinarian v) => new(v.UserId, false, true);

    /// <summary>VetA ↔ PetA (sahibi OwnerA) randevusu ekler.</summary>
    private async Task<int> AddAsync(Seeded seed, DateTime start, AppointmentStatus status = AppointmentStatus.Pending)
    {
        await using var db = fixture.NewContext();
        var a = seed.NewAppointment(start);
        a.Status = status;
        db.Appointments.Add(a);
        await db.SaveChangesAsync();
        return a.Id;
    }

    private async Task<Appointment> ReloadAsync(int id)
    {
        await using var db = fixture.NewContext();
        return await db.Appointments.SingleAsync(a => a.Id == id);
    }

    // ---------- Onay / ret ----------

    [Fact]
    public async Task Vet_approves_own_pending_appointment()
    {
        var seed = await fixture.ResetAsync();
        var id = await AddAsync(seed, At(10));
        await using var db = fixture.NewContext();

        var result = await Service(db).ApproveAsync(Vet(seed.VetA), id);

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal(AppointmentStatus.Approved, (await ReloadAsync(id)).Status);
    }

    [Fact]
    public async Task Other_vet_and_owner_cannot_approve_or_reject()
    {
        var seed = await fixture.ResetAsync();
        var id = await AddAsync(seed, At(10));
        await using var db = fixture.NewContext();
        var service = Service(db);

        var otherVet = await service.ApproveAsync(Vet(seed.VetB), id);
        var owner = await service.ApproveAsync(Member(seed.OwnerA), id);
        var otherVetReject = await service.RejectAsync(Vet(seed.VetB), id, "Uygun değilim.");

        Assert.True(otherVet.IsNotFound);
        Assert.True(owner.IsNotFound);
        Assert.True(otherVetReject.IsNotFound);
        Assert.Equal(AppointmentStatus.Pending, (await ReloadAsync(id)).Status);
    }

    [Fact]
    public async Task Admin_can_approve()
    {
        var seed = await fixture.ResetAsync();
        var id = await AddAsync(seed, At(10));
        await using var db = fixture.NewContext();

        Assert.True((await Service(db).ApproveAsync(Admin(seed.Admin), id)).Succeeded);
    }

    [Fact]
    public async Task Only_pending_can_be_approved_or_rejected()
    {
        var seed = await fixture.ResetAsync();
        var approved = await AddAsync(seed, At(10), AppointmentStatus.Approved);
        var rejected = await AddAsync(seed, At(11), AppointmentStatus.Rejected);
        await using var db = fixture.NewContext();
        var service = Service(db);

        var r1 = await service.ApproveAsync(Vet(seed.VetA), approved);
        var r2 = await service.ApproveAsync(Vet(seed.VetA), rejected);
        var r3 = await service.RejectAsync(Vet(seed.VetA), approved, "Gerekçe yazıldı.");

        Assert.All([r1, r2, r3], r => Assert.Contains("onay bekleyen", string.Join(" ", r.Errors)));
        Assert.False(r1.IsNotFound);
    }

    [Fact]
    public async Task Started_pending_appointment_cannot_be_approved()
    {
        var seed = await fixture.ResetAsync();
        var id = await AddAsync(seed, At(10));
        await using var db = fixture.NewContext();

        var result = await Service(db, Clock(At(10, 5))).ApproveAsync(Vet(seed.VetA), id);

        Assert.False(result.Succeeded);
        Assert.Contains("başlamış", string.Join(" ", result.Errors));
    }

    [Fact]
    public async Task Reject_requires_reason_and_frees_the_slot()
    {
        var seed = await fixture.ResetAsync();
        var id = await AddAsync(seed, At(10));
        await using var db = fixture.NewContext();
        var service = Service(db);

        var empty = await service.RejectAsync(Vet(seed.VetA), id, "  ");
        var ok = await service.RejectAsync(Vet(seed.VetA), id, "  O gün ameliyattayım.  ");

        Assert.Contains("Ret gerekçesi", string.Join(" ", empty.Errors));
        Assert.True(ok.Succeeded, string.Join(" ", ok.Errors));
        var saved = await ReloadAsync(id);
        Assert.Equal(AppointmentStatus.Rejected, saved.Status);
        Assert.Equal("O gün ameliyattayım.", saved.RejectionReason);
        var slots = (await service.GetAvailableSlotsAsync(seed.Examination.Id, Tuesday, seed.VetA.Id)).Single().Slots;
        Assert.Contains(At(10), slots);
    }

    // ---------- Tamamlama ----------

    [Fact]
    public async Task Vet_completes_started_approved_appointment_with_note()
    {
        var seed = await fixture.ResetAsync();
        var id = await AddAsync(seed, At(10), AppointmentStatus.Approved);
        await using var db = fixture.NewContext();
        var service = Service(db, Clock(At(10, 20)));

        var noNote = await service.CompleteAsync(Vet(seed.VetA), id, "");
        var ok = await service.CompleteAsync(Vet(seed.VetA), id, "Genel durumu iyi. Kilo takibi önerildi.");

        Assert.Contains("Muayene notu", string.Join(" ", noNote.Errors));
        Assert.True(ok.Succeeded, string.Join(" ", ok.Errors));
        var saved = await ReloadAsync(id);
        Assert.Equal(AppointmentStatus.Completed, saved.Status);
        Assert.Equal("Genel durumu iyi. Kilo takibi önerildi.", saved.ExaminationNote);
    }

    [Fact]
    public async Task Cannot_complete_future_or_pending_appointment()
    {
        var seed = await fixture.ResetAsync();
        var future = await AddAsync(seed, At(10), AppointmentStatus.Approved);
        var pending = await AddAsync(seed, At(9), AppointmentStatus.Pending);
        await using var db = fixture.NewContext();
        var service = Service(db, Clock(At(9, 30)));

        var r1 = await service.CompleteAsync(Vet(seed.VetA), future, "Not");
        var r2 = await service.CompleteAsync(Vet(seed.VetA), pending, "Not");

        Assert.Contains("başlamadan", string.Join(" ", r1.Errors));
        Assert.Contains("onaylanmış", string.Join(" ", r2.Errors));
    }

    // ---------- İptal ----------

    [Fact]
    public async Task Owner_cancels_pending_and_approved_future_appointments()
    {
        var seed = await fixture.ResetAsync();
        var pending = await AddAsync(seed, At(10));
        var approved = await AddAsync(seed, At(11), AppointmentStatus.Approved);
        await using var db = fixture.NewContext();
        var service = Service(db);

        Assert.True((await service.CancelAsync(Member(seed.OwnerA), pending)).Succeeded);
        Assert.True((await service.CancelAsync(Member(seed.OwnerA), approved)).Succeeded);
        Assert.Equal(AppointmentStatus.Cancelled, (await ReloadAsync(approved)).Status);
    }

    [Fact]
    public async Task Other_member_and_vet_cannot_cancel()
    {
        var seed = await fixture.ResetAsync();
        var id = await AddAsync(seed, At(10));
        await using var db = fixture.NewContext();
        var service = Service(db);

        Assert.True((await service.CancelAsync(Member(seed.OwnerB), id)).IsNotFound);
        Assert.True((await service.CancelAsync(Vet(seed.VetA), id)).IsNotFound);
        Assert.Equal(AppointmentStatus.Pending, (await ReloadAsync(id)).Status);
    }

    [Fact]
    public async Task Started_or_completed_appointment_cannot_be_cancelled()
    {
        var seed = await fixture.ResetAsync();
        var started = await AddAsync(seed, At(10), AppointmentStatus.Approved);
        var completed = await AddAsync(seed, At(9), AppointmentStatus.Completed);
        await using var db = fixture.NewContext();
        var service = Service(db, Clock(At(10, 5)));

        Assert.Contains("başlamış", string.Join(" ", (await service.CancelAsync(Member(seed.OwnerA), started)).Errors));
        Assert.Contains("iptal edilemez", string.Join(" ", (await service.CancelAsync(Member(seed.OwnerA), completed)).Errors));
    }

    // ---------- Görme / eşzamanlılık / panel ----------

    [Fact]
    public async Task Find_respects_visibility()
    {
        var seed = await fixture.ResetAsync();
        var id = await AddAsync(seed, At(10));
        await using var db = fixture.NewContext();
        var service = Service(db);

        Assert.NotNull(await service.FindAsync(Member(seed.OwnerA), id));
        Assert.NotNull(await service.FindAsync(Vet(seed.VetA), id));
        Assert.NotNull(await service.FindAsync(Admin(seed.Admin), id));
        Assert.Null(await service.FindAsync(Member(seed.OwnerB), id));
        Assert.Null(await service.FindAsync(Vet(seed.VetB), id));
    }

    [Fact]
    public async Task Simultaneous_approve_and_reject_only_one_applies()
    {
        var seed = await fixture.ResetAsync();
        var id = await AddAsync(seed, At(10));

        async Task<ServiceResult> Run(Func<AppointmentService, Task<ServiceResult>> action)
        {
            await using var db = fixture.NewContext();
            return await action(Service(db));
        }

        var results = await Task.WhenAll(
            Run(s => s.ApproveAsync(Vet(seed.VetA), id)),
            Run(s => s.RejectAsync(Vet(seed.VetA), id, "Müsait değilim.")),
            Run(s => s.ApproveAsync(Vet(seed.VetA), id)));

        Assert.Equal(1, results.Count(r => r.Succeeded));
    }

    [Fact]
    public async Task Vet_dashboard_groups_appointments()
    {
        var seed = await fixture.ResetAsync();
        var pending = await AddAsync(seed, At(15));
        var today = await AddAsync(seed, At(11), AppointmentStatus.Approved);
        var waiting = await AddAsync(seed, At(9), AppointmentStatus.Approved);
        var upcoming = await AddAsync(seed, At(10).AddDays(1), AppointmentStatus.Approved);
        await AddAsync(seed, At(13), AppointmentStatus.Rejected);
        await using var db = fixture.NewContext();

        var dashboard = await Service(db, Clock(At(10))).GetVetDashboardAsync(Vet(seed.VetA));

        Assert.Equal([pending], dashboard.Pending.Select(a => a.Id));
        Assert.Equal([waiting, today], dashboard.Today.Select(a => a.Id));
        Assert.Equal([upcoming], dashboard.Upcoming.Select(a => a.Id));
        Assert.Equal([waiting], dashboard.AwaitingCompletion.Select(a => a.Id));
        Assert.Empty((await Service(db, Clock(At(10))).GetVetDashboardAsync(Vet(seed.VetB))).Pending);
    }
}
