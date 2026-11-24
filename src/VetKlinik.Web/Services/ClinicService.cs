using Microsoft.EntityFrameworkCore;
using VetKlinik.Web.Data;
using VetKlinik.Web.Domain;
using VetKlinik.Web.Infrastructure;
using VetKlinik.Web.Models.Admin;

namespace VetKlinik.Web.Services;

/// <summary>Kliniğin bilgileri ve haftalık çalışma saatleri. Sistemde tek klinik vardır.</summary>
public class ClinicService(AppDbContext db)
{
    public Task<Clinic> GetAsync() =>
        db.Clinics.Include(c => c.Hours).OrderBy(c => c.Id).FirstAsync();

    public async Task<ClinicFormModel> GetFormAsync()
    {
        var clinic = await GetAsync();
        return new ClinicFormModel
        {
            Name = clinic.Name,
            Address = clinic.Address,
            Phone = clinic.Phone,
            Days = [.. TurkishDays.Week.Select(day =>
            {
                var hours = clinic.Hours.SingleOrDefault(h => h.Day == day);
                return new ClinicDayModel
                {
                    Day = day,
                    IsClosed = hours?.IsClosed ?? true,
                    Opens = hours?.Opens ?? new TimeOnly(9, 0),
                    Closes = hours?.Closes ?? new TimeOnly(18, 0),
                };
            })],
        };
    }

    public async Task<ServiceResult> UpdateAsync(ClinicFormModel form)
    {
        var errors = form.Days
            .Where(d => !d.IsClosed && d.Opens >= d.Closes)
            .Select(d => $"{TurkishDays.Name(d.Day)}: kapanış saati açılış saatinden sonra olmalıdır.")
            .ToArray();
        if (errors.Length > 0)
            return ServiceResult.Fail(errors);

        var clinic = await GetAsync();
        clinic.Name = form.Name.Trim();
        clinic.Address = form.Address.Trim();
        clinic.Phone = form.Phone.Trim();

        foreach (var day in form.Days)
        {
            var hours = clinic.Hours.SingleOrDefault(h => h.Day == day.Day);
            if (hours is null)
            {
                hours = new ClinicHours { Day = day.Day };
                clinic.Hours.Add(hours);
            }
            hours.IsClosed = day.IsClosed;
            hours.Opens = day.Opens;
            hours.Closes = day.Closes;
        }

        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }
}
