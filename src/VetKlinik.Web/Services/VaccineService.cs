using System.Globalization;
using Microsoft.EntityFrameworkCore;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Data;
using VetKlinik.Web.Domain;
using VetKlinik.Web.Infrastructure;
using VetKlinik.Web.Models.Pets;

namespace VetKlinik.Web.Services;

/// <summary>Hayvanın aşı geçmişi ve takvimi. MVC, REST API ve MCP aynı sonucu kullanır.</summary>
public record VaccineSchedule(Pet Pet, IReadOnlyList<VaccineRecord> History, IReadOnlyList<UpcomingVaccine> Upcoming);

/// <summary>Yaklaşan doz. <see cref="DaysLeft"/> negatifse doz gecikmiştir.</summary>
public record UpcomingVaccine(string VaccineName, DateOnly DueOn, int DaysLeft)
{
    public bool IsOverdue => DaysLeft < 0;
}

/// <summary>
/// Aşı kayıtları. Hayvana erişim kararı PetService'ten gelir (yetki kuralı tek yerde):
/// hayvanı görebilen (sahibi, Admin, randevusu olan hekim) aşı ekleyebilir.
/// Düzenleme/silme: Admin; kaydı giren hekim; hekim girmediyse hayvanın sahibi.
/// </summary>
public class VaccineService(AppDbContext db, PetService pets, TimeProvider time)
{
    private static readonly StringComparer TurkishIgnoreCase = StringComparer.Create(new CultureInfo("tr-TR"), ignoreCase: true);

    public async Task<VaccineSchedule?> GetScheduleAsync(CurrentUser user, int petId)
    {
        var pet = await pets.FindAsync(user, petId);
        if (pet is null)
            return null;

        var history = await db.VaccineRecords
            .Include(v => v.Veterinarian).ThenInclude(v => v!.User)
            .Where(v => v.PetId == petId)
            .OrderByDescending(v => v.AdministeredOn).ThenByDescending(v => v.Id)
            .ToListAsync();

        // Her aşı için yalnızca en son doz önemlidir: yeni doz girildiyse eskisinin tarihi geçersizdir.
        var today = ClinicTime.Today(time);
        var upcoming = history
            .GroupBy(v => v.VaccineName.Trim(), TurkishIgnoreCase)
            .Select(g => g.First()) // liste yeniden eskiye sıralı
            .Where(v => v.NextDueOn is not null)
            .Select(v => new UpcomingVaccine(v.VaccineName, v.NextDueOn!.Value, v.NextDueOn.Value.DayNumber - today.DayNumber))
            .OrderBy(u => u.DueOn)
            .ToList();

        return new VaccineSchedule(pet, history, upcoming);
    }

    public async Task<ServiceResult<VaccineRecord>> AddAsync(CurrentUser user, int petId, VaccineFormModel form)
    {
        var pet = await pets.FindAsync(user, petId);
        if (pet is null)
            return ServiceResult<VaccineRecord>.Fail("Hayvan bulunamadı.");

        var errors = Validate(form);
        if (errors.Length > 0)
            return ServiceResult<VaccineRecord>.Fail(errors);

        var record = new VaccineRecord { PetId = pet.Id, VaccineName = "" };
        if (user.IsVeterinarian)
            record.VeterinarianId = await db.Veterinarians.Where(v => v.UserId == user.Id).Select(v => (int?)v.Id).SingleOrDefaultAsync();
        Apply(record, form);

        db.VaccineRecords.Add(record);
        await db.SaveChangesAsync();
        return ServiceResult<VaccineRecord>.Ok(record);
    }

    /// <summary>Kullanıcının düzenleyebileceği kayıt (Pet dahil); değilse null.</summary>
    public Task<VaccineRecord?> FindEditableAsync(CurrentUser user, int id)
    {
        var records = db.VaccineRecords.Include(v => v.Pet).Where(v => v.Id == id);
        if (!user.IsAdmin)
            records = records.Where(v => v.VeterinarianId != null
                ? v.Veterinarian!.UserId == user.Id
                : v.Pet.OwnerId == user.Id);
        return records.SingleOrDefaultAsync();
    }

    public async Task<ServiceResult> UpdateAsync(CurrentUser user, int id, VaccineFormModel form)
    {
        var record = await FindEditableAsync(user, id);
        if (record is null)
            return ServiceResult.Fail("Aşı kaydı bulunamadı.");

        var errors = Validate(form);
        if (errors.Length > 0)
            return ServiceResult.Fail(errors);

        Apply(record, form);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(CurrentUser user, int id)
    {
        var record = await FindEditableAsync(user, id);
        if (record is null)
            return ServiceResult.Fail("Aşı kaydı bulunamadı.");

        db.VaccineRecords.Remove(record);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private string[] Validate(VaccineFormModel form)
    {
        var errors = new List<string>();
        if (form.AdministeredOn > ClinicTime.Today(time))
            errors.Add("Uygulama tarihi gelecekte olamaz.");
        if (form.NextDueOn is { } next && next <= form.AdministeredOn)
            errors.Add("Sonraki doz tarihi uygulama tarihinden sonra olmalıdır.");
        return [.. errors];
    }

    private static void Apply(VaccineRecord record, VaccineFormModel form)
    {
        record.VaccineName = form.VaccineName.Trim();
        record.AdministeredOn = form.AdministeredOn;
        record.NextDueOn = form.NextDueOn;
    }
}
