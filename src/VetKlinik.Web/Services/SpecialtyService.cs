using Microsoft.EntityFrameworkCore;
using VetKlinik.Web.Data;
using VetKlinik.Web.Domain;

namespace VetKlinik.Web.Services;

public record SpecialtyRow(int Id, string Name, int VeterinarianCount);

/// <summary>Hekim uzmanlık alanları (cerrahi, dermatoloji...).</summary>
public class SpecialtyService(AppDbContext db)
{
    public Task<List<SpecialtyRow>> ListAsync() =>
        db.Specialties
            .OrderBy(s => s.Name)
            .Select(s => new SpecialtyRow(s.Id, s.Name, s.Veterinarians.Count))
            .ToListAsync();

    public async Task<ServiceResult> CreateAsync(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > 100)
            return ServiceResult.Fail("Uzmanlık adı 1-100 karakter olmalıdır.");
        if (await db.Specialties.AnyAsync(s => EF.Functions.Collate(s.Name, "Turkish_CI_AS") == trimmed))
            return ServiceResult.Fail($"'{trimmed}' uzmanlık alanı zaten var.");

        db.Specialties.Add(new Specialty { Name = trimmed });
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var specialty = await db.Specialties.SingleOrDefaultAsync(s => s.Id == id);
        if (specialty is null)
            return ServiceResult.Fail("Uzmanlık alanı bulunamadı.");
        if (await db.Veterinarians.AnyAsync(v => v.Specialties.Any(s => s.Id == id)))
            return ServiceResult.Fail("Bu uzmanlık alanı hekimlere atanmış olduğu için silinemez.");

        db.Specialties.Remove(specialty);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }
}
