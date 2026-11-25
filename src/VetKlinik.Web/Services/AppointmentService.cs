using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Data;
using VetKlinik.Web.Domain;
using VetKlinik.Web.Infrastructure;
using VetKlinik.Web.Models.Appointments;

namespace VetKlinik.Web.Services;

/// <summary>Bir hekimin seçilen gündeki uygun başlangıç saatleri.</summary>
public record VetSlots(int VeterinarianId, string FullName, string Title, IReadOnlyList<DateTime> Slots);

/// <summary>
/// Randevu alma kuralları. Bir saatin uygun olup olmadığına tek bir fonksiyon (<see cref="SlotProblem"/>) karar verir;
/// uygun saat listesi de randevu kaydı da onu kullanır. MVC, REST API ve MCP bu servisi çağırır.
/// </summary>
public class AppointmentService(AppDbContext db, PetService pets, TimeProvider time)
{
    public const int SlotMinutes = 15;
    public const int MaxDaysAhead = 60;

    /// <summary>Saati dolduran durumlar. Reddedilen ve iptal edilen randevular saati boşaltır.</summary>
    private static readonly AppointmentStatus[] Blocking = [AppointmentStatus.Pending, AppointmentStatus.Approved];

    /// <summary>Hekimin o gün çalışabileceği aralık: hekim müsaitliği ∩ klinik saatleri.</summary>
    private sealed record Window(TimeOnly Start, TimeOnly End);

    private sealed record Busy(DateTime Start, DateTime End);

    // ---------------- Uygun saatler ----------------

    public async Task<List<VetSlots>> GetAvailableSlotsAsync(int serviceId, DateOnly date, int? veterinarianId = null)
    {
        // Geçmiş veya 60 günden uzak gün: hesaplamaya hiç girilmez (DateOnly.MaxValue gibi uç değerler de güvenli).
        var today = ClinicTime.Today(time);
        if (date < today || date > today.AddDays(MaxDaysAhead))
            return [];

        var service = await db.Services.SingleOrDefaultAsync(s => s.Id == serviceId && s.IsActive);
        if (service is null)
            return [];

        var clinicHours = await ClinicHoursAsync(date.DayOfWeek);
        var vets = await db.Veterinarians
            .Include(v => v.User)
            .Include(v => v.Availabilities)
            .Where(v => v.IsActive && v.Services.Any(s => s.Id == serviceId))
            .Where(v => veterinarianId == null || v.Id == veterinarianId)
            .OrderBy(v => v.User.FullName)
            .ToListAsync();

        var vetIds = vets.Select(v => v.Id).ToList();
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);
        var busy = await db.Appointments
            .Where(a => vetIds.Contains(a.VeterinarianId) && Blocking.Contains(a.Status)
                        && a.StartsAt < dayEnd && a.EndsAt > dayStart)
            .Select(a => new { a.VeterinarianId, a.StartsAt, a.EndsAt })
            .ToListAsync();

        var now = ClinicTime.Now(time);
        var result = new List<VetSlots>();
        foreach (var vet in vets)
        {
            var window = WindowFor(vet, clinicHours, date.DayOfWeek);
            if (window is null)
                continue;

            var vetBusy = busy.Where(b => b.VeterinarianId == vet.Id).Select(b => new Busy(b.StartsAt, b.EndsAt)).ToList();
            var slots = new List<DateTime>();
            for (var start = date.ToDateTime(RoundUpToSlot(window.Start));
                 start.AddMinutes(service.DurationMinutes) <= date.ToDateTime(window.End);
                 start = start.AddMinutes(SlotMinutes))
            {
                if (SlotProblem(start, service.DurationMinutes, window, vetBusy, now) is null)
                    slots.Add(start);
            }

            if (slots.Count > 0)
                result.Add(new VetSlots(vet.Id, vet.User.FullName, vet.Title, slots));
        }
        return result;
    }

    // ---------------- Randevu alma ----------------

    public async Task<ServiceResult<Appointment>> BookAsync(CurrentUser user, BookingFormModel form)
    {
        // Yalnız hayvanı düzenleyebilen (sahibi veya Admin) randevu alabilir.
        var pet = await pets.FindEditableAsync(user, form.PetId);
        if (pet is null)
            return ServiceResult<Appointment>.Fail("Hayvan bulunamadı.");

        var service = await db.Services.SingleOrDefaultAsync(s => s.Id == form.ServiceId && s.IsActive);
        if (service is null)
            return ServiceResult<Appointment>.Fail("Hizmet bulunamadı veya şu an verilmiyor.");

        var vet = await db.Veterinarians
            .Include(v => v.User)
            .Include(v => v.Availabilities)
            .Include(v => v.Services)
            .SingleOrDefaultAsync(v => v.Id == form.VeterinarianId && v.IsActive);
        if (vet is null)
            return ServiceResult<Appointment>.Fail("Hekim bulunamadı.");
        if (vet.Services.All(s => s.Id != service.Id))
            return ServiceResult<Appointment>.Fail($"{vet.Title} {vet.User.FullName} bu hizmeti vermiyor.");

        if (form.StartsAt is null)
            return ServiceResult<Appointment>.Fail("Saat seçiniz.");

        // API/MCP'den UTC veya saat dilimli zaman gelebilir: önce klinik saatine çevrilir.
        var start = DateTime.SpecifyKind(ClinicTime.ToClinic(form.StartsAt.Value), DateTimeKind.Unspecified);
        var today = ClinicTime.Today(time);
        if (DateOnly.FromDateTime(start) > today.AddDays(MaxDaysAhead))
            return ServiceResult<Appointment>.Fail($"En fazla {MaxDaysAhead} gün sonrasına randevu alınabilir.");
        if (DateOnly.FromDateTime(start) < today)
            return ServiceResult<Appointment>.Fail("Seçilen saat geçmişte kaldı; ileri bir saat seçiniz.");

        var end = start.AddMinutes(service.DurationMinutes);
        var window = WindowFor(vet, await ClinicHoursAsync(start.DayOfWeek), start.DayOfWeek);

        // (Hekim/hizmet bilgisi yukarıda transaction dışında okunur; admin tam bu anda pasifleştirirse randevu yine alınabilir — kabul edilen küçük risk.)
        // Serializable: çakışma kontrolü ile ekleme arasında başka bir işlem aynı aralığa randevu ekleyemez.
        // Aynı anda gelen iki istekten biri SQL Server tarafından durdurulur (deadlock kurbanı) ve aşağıda yakalanır.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var vetBusy = await db.Appointments
                .Where(a => a.VeterinarianId == vet.Id && Blocking.Contains(a.Status) && a.StartsAt < end && start < a.EndsAt)
                .Select(a => new Busy(a.StartsAt, a.EndsAt))
                .ToListAsync();

            var problem = SlotProblem(start, service.DurationMinutes, window, vetBusy, ClinicTime.Now(time));
            if (problem is not null)
                return ServiceResult<Appointment>.Fail(problem);

            if (await db.Appointments.AnyAsync(a => a.PetId == pet.Id && Blocking.Contains(a.Status) && a.StartsAt < end && start < a.EndsAt))
                return ServiceResult<Appointment>.Fail($"{pet.Name} için bu saatlerde başka bir randevu var.");

            var appointment = new Appointment
            {
                PetId = pet.Id,
                VeterinarianId = vet.Id,
                ServiceId = service.Id,
                StartsAt = start,
                EndsAt = end,
                DurationMinutes = service.DurationMinutes,
                Price = service.Price,
                Status = AppointmentStatus.Pending,
                CreatedAt = ClinicTime.Now(time),
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return ServiceResult<Appointment>.Ok(appointment);
        }
        catch (Exception ex) when (IsConcurrencyConflict(ex))
        {
            db.ChangeTracker.Clear();
            return ServiceResult<Appointment>.Fail("Bu saat az önce başka bir randevuyla doldu. Lütfen başka bir saat seçin.");
        }
    }

    // ---------------- Listeleme ----------------

    /// <summary>Admin: hepsi. Hekim: kendi randevuları. Üye: kendi hayvanlarının randevuları.</summary>
    public Task<List<Appointment>> ListAsync(CurrentUser user)
    {
        var query = db.Appointments
            .Include(a => a.Pet)
            .Include(a => a.Service)
            .Include(a => a.Veterinarian).ThenInclude(v => v.User)
            .AsQueryable();

        if (!user.IsAdmin)
            query = user.IsVeterinarian
                ? query.Where(a => a.Veterinarian.UserId == user.Id)
                : query.Where(a => a.Pet.OwnerId == user.Id);

        return query.OrderByDescending(a => a.StartsAt).ToListAsync();
    }

    // ---------------- Kurallar ----------------

    /// <summary>
    /// Saat uygunsa null, değilse nedeni (Türkçe). Liste ve kayıt aynı fonksiyonu kullanır.
    /// Çakışma: a.Start &lt; b.End && b.Start &lt; a.End (uç uca randevular çakışmaz).
    /// </summary>
    private static string? SlotProblem(DateTime start, int durationMinutes, Window? window, IEnumerable<Busy> busy, DateTime now)
    {
        var end = start.AddMinutes(durationMinutes);
        if (start.Ticks % TimeSpan.FromMinutes(SlotMinutes).Ticks != 0)
            return "Randevu saatleri 15 dakikalık aralıklarla başlar (ör. 10:00, 10:15).";
        if (start <= now)
            return "Seçilen saat geçmişte kaldı; ileri bir saat seçiniz.";
        if (DateOnly.FromDateTime(start) > DateOnly.FromDateTime(now).AddDays(MaxDaysAhead))
            return $"En fazla {MaxDaysAhead} gün sonrasına randevu alınabilir.";
        if (window is null)
            return "Hekim bu gün çalışmıyor veya klinik kapalı (çalışma saatleri dışında).";
        if (end.Date != start.Date || TimeOnly.FromDateTime(start) < window.Start || TimeOnly.FromDateTime(end) > window.End)
            return $"Seçilen saat hekimin çalışma saatleri ({window.Start:HH\\:mm}–{window.End:HH\\:mm}) dışında; işlem {durationMinutes} dakika sürer.";
        if (busy.Any(b => start < b.End && b.Start < end))
            return "Hekimin bu saati dolu. Lütfen başka bir saat seçin.";
        return null;
    }

    /// <summary>09:10 gibi çeyreğe denk gelmeyen başlangıç 09:15'e yuvarlanır (liste ve kayıt aynı saatleri kabul etsin).</summary>
    private static TimeOnly RoundUpToSlot(TimeOnly time)
    {
        var remainder = time.Ticks % TimeSpan.FromMinutes(SlotMinutes).Ticks;
        return remainder == 0 || time.Ticks - remainder + TimeSpan.FromMinutes(SlotMinutes).Ticks >= TimeSpan.TicksPerDay
            ? time
            : new TimeOnly(time.Ticks - remainder + TimeSpan.FromMinutes(SlotMinutes).Ticks);
    }

    private static Window? WindowFor(Veterinarian vet, ClinicHours? clinicHours, DayOfWeek day)
    {
        var availability = vet.Availabilities.FirstOrDefault(a => a.Day == day);
        if (clinicHours is null || clinicHours.IsClosed || availability is null)
            return null;

        var start = availability.Starts > clinicHours.Opens ? availability.Starts : clinicHours.Opens;
        var end = availability.Ends < clinicHours.Closes ? availability.Ends : clinicHours.Closes;
        return start < end ? new Window(start, end) : null;
    }

    private Task<ClinicHours?> ClinicHoursAsync(DayOfWeek day) =>
        db.Clinics.OrderBy(c => c.Id).SelectMany(c => c.Hours).SingleOrDefaultAsync(h => h.Day == day);

    /// <summary>1205: deadlock kurbanı (Serializable altında eşzamanlı ekleme); 2601/2627: benzersizlik ihlali.</summary>
    private static bool IsConcurrencyConflict(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
            if (e is SqlException { Number: 1205 or 2601 or 2627 })
                return true;
        return false;
    }
}
