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

/// <summary>Hekim paneli: işlem bekleyen ve yaklaşan randevular.</summary>
public record VetDashboard(
    IReadOnlyList<Appointment> Pending,
    IReadOnlyList<Appointment> Today,
    IReadOnlyList<Appointment> Upcoming,
    IReadOnlyList<Appointment> AwaitingCompletion,
    IReadOnlyList<Appointment> Expired);

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
            return ServiceResult<Appointment>.NotFound("Hayvan bulunamadı.");

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

    // ---------------- Görme ve listeleme ----------------

    /// <summary>Admin: hepsi. Hekim: kendi randevuları. Üye: kendi hayvanlarının randevuları.</summary>
    private IQueryable<Appointment> Visible(CurrentUser user)
    {
        var query = db.Appointments
            .Include(a => a.Pet).ThenInclude(p => p.Owner)
            .Include(a => a.Service)
            .Include(a => a.Veterinarian).ThenInclude(v => v.User)
            .AsQueryable();

        if (user.IsAdmin)
            return query;
        return user.IsVeterinarian
            ? query.Where(a => a.Veterinarian.UserId == user.Id)
            : query.Where(a => a.Pet.OwnerId == user.Id);
    }

    public Task<List<Appointment>> ListAsync(CurrentUser user) =>
        Visible(user).OrderByDescending(a => a.StartsAt).ToListAsync();

    public Task<Appointment?> FindAsync(CurrentUser user, int id) =>
        Visible(user).SingleOrDefaultAsync(a => a.Id == id);

    public async Task<VetDashboard> GetVetDashboardAsync(CurrentUser user)
    {
        var now = ClinicTime.Now(time);
        var todayStart = now.Date;
        var tomorrow = todayStart.AddDays(1);
        var list = await Visible(user)
            .Where(a => a.Veterinarian.UserId == user.Id
                        && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Approved))
            .OrderBy(a => a.StartsAt)
            .ToListAsync();

        return new VetDashboard(
            Pending: [.. list.Where(a => a.Status == AppointmentStatus.Pending && a.StartsAt > now)],
            Today: [.. list.Where(a => a.Status == AppointmentStatus.Approved && a.StartsAt >= todayStart && a.StartsAt < tomorrow)],
            Upcoming: [.. list.Where(a => a.Status == AppointmentStatus.Approved && a.StartsAt >= tomorrow)],
            AwaitingCompletion: [.. list.Where(a => a.Status == AppointmentStatus.Approved && a.StartsAt <= now)],
            // Zamanında onaylanmamış talepler: artık onaylanamaz ama hekim reddederek kapatabilir.
            Expired: [.. list.Where(a => a.Status == AppointmentStatus.Pending && a.StartsAt <= now)]);
    }

    // ---------------- Durum geçişleri ----------------
    // Pending → Approved / Rejected (hekim), Approved → Completed (hekim), Pending/Approved → Cancelled (sahip).

    public Task<ServiceResult> ApproveAsync(CurrentUser user, int id) =>
        TransitionAsync(user, id, IsVetOf, (a, now) =>
        {
            if (a.Status != AppointmentStatus.Pending)
                return "Yalnızca onay bekleyen randevular onaylanabilir.";
            if (a.StartsAt <= now)
                return "Randevu başlamış veya geçmişte kaldığı için onaylanamaz.";
            a.Status = AppointmentStatus.Approved;
            return null;
        });

    public Task<ServiceResult> RejectAsync(CurrentUser user, int id, string? reason) =>
        TransitionAsync(user, id, IsVetOf, (a, _) =>
        {
            var text = reason?.Trim() ?? "";
            if (text.Length is < 5 or > 500)
                return "Ret gerekçesi 5 ile 500 karakter arasında olmalıdır.";
            if (a.Status != AppointmentStatus.Pending)
                return "Yalnızca onay bekleyen randevular reddedilebilir.";
            a.Status = AppointmentStatus.Rejected;
            a.RejectionReason = text;
            return null;
        });

    public Task<ServiceResult> CompleteAsync(CurrentUser user, int id, string? note) =>
        TransitionAsync(user, id, IsVetOf, (a, now) =>
        {
            var text = note?.Trim() ?? "";
            if (text.Length is < 1 or > 2000)
                return "Muayene notu zorunludur (en fazla 2000 karakter).";
            if (a.Status != AppointmentStatus.Approved)
                return "Yalnızca onaylanmış randevular tamamlanabilir.";
            if (a.StartsAt > now)
                return "Randevu başlamadan tamamlanamaz.";
            a.Status = AppointmentStatus.Completed;
            a.ExaminationNote = text;
            return null;
        });

    public Task<ServiceResult> CancelAsync(CurrentUser user, int id) =>
        TransitionAsync(user, id, IsOwnerOf, (a, now) =>
        {
            if (a.Status is not (AppointmentStatus.Pending or AppointmentStatus.Approved))
                return "Bu randevu iptal edilemez.";
            if (a.StartsAt <= now)
                return "Randevu başlamış veya geçmişte kaldığı için iptal edilemez.";
            a.Status = AppointmentStatus.Cancelled;
            return null;
        });

    private static bool IsVetOf(CurrentUser user, Appointment a) => user.IsAdmin || a.Veterinarian.UserId == user.Id;

    private static bool IsOwnerOf(CurrentUser user, Appointment a) => user.IsAdmin || (!user.IsVeterinarian && a.Pet.OwnerId == user.Id);

    /// <summary>
    /// Ortak geçiş akışı: yükle → yetki (yoksa 404) → kural (change null değilse hata) → kaydet.
    /// RowVersion: aynı randevu aynı anda iki kez değiştirilirse ikincisi DbUpdateConcurrencyException alır.
    /// </summary>
    private async Task<ServiceResult> TransitionAsync(
        CurrentUser user, int id, Func<CurrentUser, Appointment, bool> canAct, Func<Appointment, DateTime, string?> change)
    {
        var appointment = await db.Appointments
            .Include(a => a.Pet)
            .Include(a => a.Veterinarian)
            .SingleOrDefaultAsync(a => a.Id == id);
        if (appointment is null || !canAct(user, appointment))
            return ServiceResult.NotFound("Randevu bulunamadı.");

        var problem = change(appointment, ClinicTime.Now(time));
        if (problem is not null)
            return ServiceResult.Fail(problem);

        try
        {
            await db.SaveChangesAsync();
            return ServiceResult.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return ServiceResult.Fail("Randevu bu arada değiştirildi. Sayfayı yenileyip tekrar deneyin.");
        }
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
