using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Data;
using VetKlinik.Web.Domain;
using VetKlinik.Web.Infrastructure;
using VetKlinik.Web.Models.Admin;

namespace VetKlinik.Web.Services;

/// <summary>Hekimler: giriş hesabı, uzmanlıklar, verdiği hizmetler ve haftalık müsaitlik.</summary>
public class VeterinarianService(AppDbContext db, UserManager<ApplicationUser> userManager, ClinicService clinics)
{
    public Task<List<Veterinarian>> ListAsync() =>
        db.Veterinarians
            .Include(v => v.User)
            .Include(v => v.Specialties)
            .Include(v => v.Services)
            .OrderBy(v => v.User.FullName)
            .AsSplitQuery()
            .ToListAsync();

    public Task<Veterinarian?> FindAsync(int id) =>
        db.Veterinarians
            .Include(v => v.User)
            .Include(v => v.Specialties)
            .Include(v => v.Services)
            .Include(v => v.Availabilities)
            .AsSplitQuery()
            .SingleOrDefaultAsync(v => v.Id == id);

    public Task<VeterinarianFormModel> NewFormAsync() => Task.FromResult(new VeterinarianFormModel
    {
        Days = [.. TurkishDays.Week.Select(day => new AvailabilityDayModel { Day = day })],
    });

    public async Task<VeterinarianFormModel?> GetFormAsync(int id)
    {
        var vet = await FindAsync(id);
        if (vet is null)
            return null;

        return new VeterinarianFormModel
        {
            FullName = vet.User.FullName,
            Email = vet.User.Email ?? "",
            Title = vet.Title,
            IsActive = vet.IsActive,
            SpecialtyIds = [.. vet.Specialties.Select(s => s.Id)],
            ServiceIds = [.. vet.Services.Select(s => s.Id)],
            Days = [.. TurkishDays.Week.Select(day =>
            {
                var slot = vet.Availabilities.FirstOrDefault(a => a.Day == day);
                return new AvailabilityDayModel { Day = day, Works = slot is not null, Starts = slot?.Starts, Ends = slot?.Ends };
            })],
        };
    }

    public async Task<ServiceResult<Veterinarian>> CreateAsync(VeterinarianFormModel form)
    {
        if (string.IsNullOrWhiteSpace(form.Password))
            return ServiceResult<Veterinarian>.Fail("Parola zorunludur.");
        var errors = await ValidateAvailabilityAsync(form.Days);
        if (errors.Length > 0)
            return ServiceResult<Veterinarian>.Fail(errors);

        // Kullanıcı hesabı ve hekim kaydı birlikte oluşur ya da hiçbiri oluşmaz.
        await using var transaction = await db.Database.BeginTransactionAsync();

        var user = new ApplicationUser
        {
            UserName = form.Email.Trim(),
            Email = form.Email.Trim(),
            EmailConfirmed = true,
            FullName = form.FullName.Trim(),
        };
        var created = await userManager.CreateAsync(user, form.Password);
        if (!created.Succeeded)
            return ServiceResult<Veterinarian>.Fail([.. created.Errors.Select(e => e.Description).Distinct()]);
        var role = await userManager.AddToRoleAsync(user, Roles.Veteriner);
        if (!role.Succeeded)
            return ServiceResult<Veterinarian>.Fail([.. role.Errors.Select(e => e.Description)]);

        var vet = new Veterinarian { UserId = user.Id, Title = form.Title.Trim(), IsActive = form.IsActive };
        await ApplyRelationsAsync(vet, form);
        db.Veterinarians.Add(vet);
        await db.SaveChangesAsync();

        await transaction.CommitAsync();
        return ServiceResult<Veterinarian>.Ok(vet);
    }

    public async Task<ServiceResult> UpdateAsync(int id, VeterinarianFormModel form)
    {
        var vet = await FindAsync(id);
        if (vet is null)
            return ServiceResult.Fail("Hekim bulunamadı.");
        var errors = await ValidateAvailabilityAsync(form.Days);
        if (errors.Length > 0)
            return ServiceResult.Fail(errors);

        // E-posta giriş adı olduğu için düzenlemede değişmez.
        vet.User.FullName = form.FullName.Trim();
        vet.Title = form.Title.Trim();
        vet.IsActive = form.IsActive;
        await ApplyRelationsAsync(vet, form);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var vet = await FindAsync(id);
        if (vet is null)
            return ServiceResult.Fail("Hekim bulunamadı.");
        if (await db.Appointments.AnyAsync(a => a.VeterinarianId == id))
            return ServiceResult.Fail("Bu hekime ait randevular olduğu için silinemez. Bunun yerine pasif hale getirebilirsiniz.");

        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Veterinarians.Remove(vet);
        await db.SaveChangesAsync();
        var deleted = await userManager.DeleteAsync(vet.User);
        if (!deleted.Succeeded)
            return ServiceResult.Fail([.. deleted.Errors.Select(e => e.Description)]);
        await transaction.CommitAsync();
        return ServiceResult.Ok();
    }

    /// <summary>Çalışılan her gün: başlangıç bitişten önce ve kliniğin o günkü saatleri içinde olmalı.</summary>
    private async Task<string[]> ValidateAvailabilityAsync(IEnumerable<AvailabilityDayModel> days)
    {
        var clinic = await clinics.GetAsync();
        var errors = new List<string>();
        foreach (var day in days.Where(d => d.Works))
        {
            var name = TurkishDays.Name(day.Day);
            var hours = clinic.Hours.SingleOrDefault(h => h.Day == day.Day);
            if (day.Starts is null || day.Ends is null)
                errors.Add($"{name}: başlangıç ve bitiş saatini girin.");
            else if (day.Starts >= day.Ends)
                errors.Add($"{name}: bitiş saati başlangıç saatinden sonra olmalıdır.");
            else if (hours is null || hours.IsClosed)
                errors.Add($"{name}: klinik bu gün kapalı.");
            else if (day.Starts < hours.Opens || day.Ends > hours.Closes)
                errors.Add($"{name}: çalışma saati klinik saatleri ({hours.Opens:HH\\:mm}–{hours.Closes:HH\\:mm}) içinde olmalıdır.");
        }
        return [.. errors];
    }

    /// <summary>Formdaki seçimleri uygular; var olmayan id'ler yok sayılır.</summary>
    private async Task ApplyRelationsAsync(Veterinarian vet, VeterinarianFormModel form)
    {
        var specialties = await db.Specialties.Where(s => form.SpecialtyIds.Contains(s.Id)).ToListAsync();
        var services = await db.Services.Where(s => form.ServiceIds.Contains(s.Id)).ToListAsync();

        vet.Specialties.Clear();
        vet.Specialties.AddRange(specialties);
        vet.Services.Clear();
        vet.Services.AddRange(services);
        vet.Availabilities.Clear();
        vet.Availabilities.AddRange(form.Days
            .Where(d => d.Works)
            .Select(d => new VetAvailability { Day = d.Day, Starts = d.Starts!.Value, Ends = d.Ends!.Value }));
    }
}
