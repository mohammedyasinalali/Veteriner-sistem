using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using VetKlinik.Tests.Infrastructure;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Data;
using VetKlinik.Web.Domain;
using VetKlinik.Web.Models.Appointments;
using VetKlinik.Web.Services;

namespace VetKlinik.Tests.Services;

public class AppointmentServiceTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    // Şimdi: Pazartesi 7 Aralık 2026, 08:00 (Istanbul). Test kliniği hafta içi 09–18 açık; iki hekim de hafta içi 09–18.
    private static readonly DateOnly Monday = new(2026, 12, 7);
    private static readonly DateOnly Tuesday = new(2026, 12, 8);

    private static FakeTimeProvider ClockAt(int hour, int minute = 0) =>
        new(new DateTimeOffset(2026, 12, 7, hour, minute, 0, TimeSpan.FromHours(3)));

    private static AppointmentService Service(AppDbContext db, FakeTimeProvider? clock = null)
    {
        clock ??= ClockAt(8);
        return new AppointmentService(db, new PetService(db, new PhotoStorage(Path.GetTempPath()), clock), clock);
    }

    private static CurrentUser Member(ApplicationUser u) => new(u.Id, false, false);

    private static DateTime At(DateOnly day, int hour, int minute = 0) => day.ToDateTime(new TimeOnly(hour, minute));

    private static BookingFormModel Booking(Seeded seed, DateTime start, Service? service = null, Veterinarian? vet = null, Pet? pet = null) => new()
    {
        PetId = (pet ?? seed.PetA).Id,
        ServiceId = (service ?? seed.Examination).Id,
        VeterinarianId = (vet ?? seed.VetA).Id,
        StartsAt = start,
    };

    private async Task AddExistingAsync(Seeded seed, DateTime start, AppointmentStatus status = AppointmentStatus.Pending, Pet? pet = null, Veterinarian? vet = null)
    {
        await using var db = fixture.NewContext();
        var a = seed.NewAppointment(start);
        a.Status = status;
        a.PetId = (pet ?? seed.PetB).Id;
        a.VeterinarianId = (vet ?? seed.VetA).Id;
        db.Appointments.Add(a);
        await db.SaveChangesAsync();
    }

    // ---------- Uygun saatler ----------

    [Fact]
    public async Task Slots_cover_the_working_day_in_15_minute_steps()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var result = await Service(db).GetAvailableSlotsAsync(seed.Examination.Id, Tuesday);

        var vetA = result.Single(v => v.VeterinarianId == seed.VetA.Id);
        Assert.Equal(At(Tuesday, 9), vetA.Slots[0]);
        Assert.Equal(At(Tuesday, 9, 15), vetA.Slots[1]);
        Assert.Equal(At(Tuesday, 17, 30), vetA.Slots[^1]); // 17:30 + 30 dk = 18:00
        Assert.Equal(35, vetA.Slots.Count);
        Assert.Equal("Hekim A", vetA.FullName);
    }

    [Fact]
    public async Task Slots_respect_service_duration_against_existing_appointment()
    {
        var seed = await fixture.ResetAsync();
        await AddExistingAsync(seed, At(Tuesday, 10)); // VetA 10:00–10:30

        await using var db = fixture.NewContext();
        var slots = (await Service(db).GetAvailableSlotsAsync(seed.Neutering.Id, Tuesday)).Single().Slots;

        Assert.DoesNotContain(At(Tuesday, 9), slots);     // 09:00–10:30 çakışır (90 dk)
        Assert.DoesNotContain(At(Tuesday, 8, 45), slots);
        Assert.DoesNotContain(At(Tuesday, 10, 15), slots);
        Assert.Contains(At(Tuesday, 10, 30), slots);      // biter bitmez başlayabilir
        Assert.Equal(At(Tuesday, 16, 30), slots[^1]);     // 16:30 + 90 dk = 18:00
    }

    [Fact]
    public async Task Rejected_and_cancelled_appointments_free_the_slot()
    {
        var seed = await fixture.ResetAsync();
        await AddExistingAsync(seed, At(Tuesday, 10), AppointmentStatus.Rejected);
        await AddExistingAsync(seed, At(Tuesday, 11), AppointmentStatus.Cancelled);
        await AddExistingAsync(seed, At(Tuesday, 12), AppointmentStatus.Approved);

        await using var db = fixture.NewContext();
        var slots = (await Service(db).GetAvailableSlotsAsync(seed.Examination.Id, Tuesday, seed.VetA.Id)).Single().Slots;

        Assert.Contains(At(Tuesday, 10), slots);
        Assert.Contains(At(Tuesday, 11), slots);
        Assert.DoesNotContain(At(Tuesday, 12), slots);
    }

    [Fact]
    public async Task Past_times_today_are_not_offered()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var slots = (await Service(db, ClockAt(10, 5)).GetAvailableSlotsAsync(seed.Examination.Id, Monday, seed.VetA.Id)).Single().Slots;

        Assert.Equal(At(Monday, 10, 15), slots[0]);
    }

    [Fact]
    public async Task Only_active_vets_offering_the_service_on_open_days_are_listed()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();
        var service = Service(db);

        Assert.Equal([seed.VetA.Id], (await service.GetAvailableSlotsAsync(seed.Neutering.Id, Tuesday)).Select(v => v.VeterinarianId));
        Assert.Empty(await service.GetAvailableSlotsAsync(seed.Examination.Id, new DateOnly(2026, 12, 13))); // Pazar
        Assert.Empty(await service.GetAvailableSlotsAsync(seed.Examination.Id, new DateOnly(2026, 12, 6)));  // geçmiş gün

        await db.Veterinarians.Where(v => v.Id == seed.VetB.Id).ExecuteUpdateAsync(s => s.SetProperty(v => v.IsActive, false));
        Assert.Equal([seed.VetA.Id], (await service.GetAvailableSlotsAsync(seed.Examination.Id, Tuesday)).Select(v => v.VeterinarianId));
    }

    // ---------- Randevu alma ----------

    [Fact]
    public async Task Book_creates_pending_appointment_with_copied_duration_and_price()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var result = await Service(db).BookAsync(Member(seed.OwnerA), Booking(seed, At(Tuesday, 14), seed.Neutering));

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        await using var fresh = fixture.NewContext();
        var a = await fresh.Appointments.SingleAsync();
        Assert.Equal(AppointmentStatus.Pending, a.Status);
        Assert.Equal(At(Tuesday, 15, 30), a.EndsAt);
        Assert.Equal(90, a.DurationMinutes);
        Assert.Equal(3000m, a.Price);
        Assert.Equal(At(Monday, 8), a.CreatedAt);
    }

    [Fact]
    public async Task Book_rejects_overlap_with_vets_existing_appointment()
    {
        var seed = await fixture.ResetAsync();
        await AddExistingAsync(seed, At(Tuesday, 10)); // VetA 10:00–10:30
        await using var db = fixture.NewContext();

        var result = await Service(db).BookAsync(Member(seed.OwnerA), Booking(seed, At(Tuesday, 9), seed.Neutering)); // 09:00–10:30

        Assert.False(result.Succeeded);
        Assert.Contains("dolu", string.Join(" ", result.Errors));
    }

    [Fact]
    public async Task Book_rejects_overlap_for_the_same_pet_with_another_vet()
    {
        var seed = await fixture.ResetAsync();
        await AddExistingAsync(seed, At(Tuesday, 10), pet: seed.PetA, vet: seed.VetB);
        await using var db = fixture.NewContext();

        var result = await Service(db).BookAsync(Member(seed.OwnerA), Booking(seed, At(Tuesday, 10, 15)));

        Assert.False(result.Succeeded);
        Assert.Contains("Pamuk", string.Join(" ", result.Errors));
    }

    [Fact]
    public async Task Book_rejects_other_members_pet()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var result = await Service(db).BookAsync(Member(seed.OwnerB), Booking(seed, At(Tuesday, 10)));

        Assert.False(result.Succeeded);
        Assert.Equal(["Hayvan bulunamadı."], result.Errors);
        Assert.False(await db.Appointments.AnyAsync());
    }

    [Theory]
    [InlineData(17, 45, "çalışma saatleri")]  // 17:45–18:15 mesai dışına taşar
    [InlineData(8, 30, "çalışma saatleri")]
    [InlineData(10, 10, "15 dakika")]
    public async Task Book_rejects_invalid_times(int hour, int minute, string expected)
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var result = await Service(db).BookAsync(Member(seed.OwnerA), Booking(seed, At(Tuesday, hour, minute)));

        Assert.False(result.Succeeded);
        Assert.Contains(expected, string.Join(" ", result.Errors));
    }

    [Fact]
    public async Task Book_rejects_past_and_far_future()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();
        var service = Service(db, ClockAt(12));

        var past = await service.BookAsync(Member(seed.OwnerA), Booking(seed, At(Monday, 11)));
        var far = await service.BookAsync(Member(seed.OwnerA), Booking(seed, At(Monday.AddDays(63), 10)));

        Assert.Contains("geçmiş", string.Join(" ", past.Errors));
        Assert.Contains("60 gün", string.Join(" ", far.Errors));
    }

    [Fact]
    public async Task Book_rejects_vet_not_offering_service_and_inactive_service()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();
        var service = Service(db);

        var notOffered = await service.BookAsync(Member(seed.OwnerA), Booking(seed, At(Tuesday, 10), seed.Neutering, seed.VetB));
        await db.Services.Where(s => s.Id == seed.Examination.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        var inactive = await service.BookAsync(Member(seed.OwnerA), Booking(seed, At(Tuesday, 10)));

        Assert.Contains("bu hizmeti vermiyor", string.Join(" ", notOffered.Errors));
        Assert.Contains("Hizmet bulunamadı", string.Join(" ", inactive.Errors));
    }

    [Fact]
    public async Task Every_listed_slot_can_be_booked()
    {
        var seed = await fixture.ResetAsync();
        await AddExistingAsync(seed, At(Tuesday, 13));
        await using var db = fixture.NewContext();
        var service = Service(db);
        var slots = (await service.GetAvailableSlotsAsync(seed.Neutering.Id, Tuesday)).Single().Slots;

        // İlk ve son uygun saat de, çakışan randevunun hemen önündeki saat de kabul edilir.
        foreach (var start in new[] { slots[0], slots[^1], At(Tuesday, 11, 30) })
        {
            Assert.Contains(start, slots);
            var result = await service.BookAsync(Member(seed.OwnerA), Booking(seed, start, seed.Neutering));
            Assert.True(result.Succeeded, $"{start}: {string.Join(" ", result.Errors)}");
            await db.Appointments.Where(a => a.PetId == seed.PetA.Id).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task Concurrent_bookings_for_the_same_slot_only_one_succeeds()
    {
        var seed = await fixture.ResetAsync();

        async Task<ServiceResult> BookInOwnContext(Pet pet, ApplicationUser owner)
        {
            await using var db = fixture.NewContext();
            return await Service(db).BookAsync(Member(owner), Booking(seed, At(Tuesday, 10), pet: pet));
        }

        var results = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(i => i % 2 == 0 ? BookInOwnContext(seed.PetA, seed.OwnerA) : BookInOwnContext(seed.PetB, seed.OwnerB)));

        Assert.Equal(1, results.Count(r => r.Succeeded));
        await using var fresh = fixture.NewContext();
        Assert.Equal(1, await fresh.Appointments.CountAsync());
    }

    [Fact]
    public async Task Member_lists_only_appointments_of_own_pets()
    {
        var seed = await fixture.ResetAsync();
        await AddExistingAsync(seed, At(Tuesday, 10), pet: seed.PetA);
        await AddExistingAsync(seed, At(Tuesday, 11), pet: seed.PetB);
        await using var db = fixture.NewContext();

        var list = await Service(db).ListAsync(Member(seed.OwnerA));

        Assert.Equal([seed.PetA.Id], list.Select(a => a.PetId));
        Assert.NotNull(list[0].Service);
        Assert.NotNull(list[0].Veterinarian.User);
    }

    // ---------- İnceleme sonrası eklenen sınır durumları ----------

    [Fact]
    public async Task Unaligned_availability_start_is_rounded_up_to_quarter_hour()
    {
        var seed = await fixture.ResetAsync();
        await using (var setup = fixture.NewContext())
            await setup.Set<VetAvailability>()
                .Where(a => a.VeterinarianId == seed.VetA.Id && a.Day == DayOfWeek.Tuesday)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Starts, new TimeOnly(9, 10)));
        await using var db = fixture.NewContext();
        var service = Service(db);

        var slots = (await service.GetAvailableSlotsAsync(seed.Examination.Id, Tuesday, seed.VetA.Id)).Single().Slots;

        Assert.Equal(At(Tuesday, 9, 15), slots[0]);
        Assert.True((await service.BookAsync(Member(seed.OwnerA), Booking(seed, slots[0]))).Succeeded);
    }

    [Fact]
    public async Task Out_of_range_dates_fail_without_exception()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();
        var service = Service(db);

        Assert.Empty(await service.GetAvailableSlotsAsync(seed.Examination.Id, DateOnly.MaxValue));
        Assert.Empty(await service.GetAvailableSlotsAsync(seed.Examination.Id, DateOnly.MinValue));
        var max = await service.BookAsync(Member(seed.OwnerA), Booking(seed, new DateTime(9999, 12, 31, 23, 45, 0)));
        var min = await service.BookAsync(Member(seed.OwnerA), new BookingFormModel { PetId = seed.PetA.Id, ServiceId = seed.Examination.Id, VeterinarianId = seed.VetA.Id });

        Assert.Contains("60 gün", string.Join(" ", max.Errors));
        Assert.False(min.Succeeded);
    }

    [Fact]
    public async Task Utc_start_is_converted_to_clinic_time()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        // API/MCP istemcisi "2026-12-08T07:00:00Z" gönderirse randevu Istanbul saatiyle 10:00'dadır.
        var result = await Service(db).BookAsync(Member(seed.OwnerA), Booking(seed, new DateTime(2026, 12, 8, 7, 0, 0, DateTimeKind.Utc)));

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        await using var fresh = fixture.NewContext();
        Assert.Equal(At(Tuesday, 10), (await fresh.Appointments.SingleAsync()).StartsAt);
    }

    [Fact]
    public async Task Sub_second_start_is_rejected()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var result = await Service(db).BookAsync(Member(seed.OwnerA), Booking(seed, At(Tuesday, 10).AddTicks(1)));

        Assert.Contains("15 dakika", string.Join(" ", result.Errors));
    }

    [Fact]
    public async Task Concurrent_bookings_of_same_pet_with_two_vets_only_one_succeeds()
    {
        var seed = await fixture.ResetAsync();

        async Task<ServiceResult> BookWith(Veterinarian vet)
        {
            await using var db = fixture.NewContext();
            return await Service(db).BookAsync(Member(seed.OwnerA), Booking(seed, At(Tuesday, 11), vet: vet));
        }

        var results = await Task.WhenAll(BookWith(seed.VetA), BookWith(seed.VetB), BookWith(seed.VetA), BookWith(seed.VetB));

        Assert.Equal(1, results.Count(r => r.Succeeded));
    }
}
