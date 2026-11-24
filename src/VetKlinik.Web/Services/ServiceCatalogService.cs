using Microsoft.EntityFrameworkCore;
using VetKlinik.Web.Data;
using VetKlinik.Web.Domain;
using VetKlinik.Web.Models.Admin;

namespace VetKlinik.Web.Services;

/// <summary>Klinik hizmetleri (muayene, aşı...) ile ilgili iş kuralları.</summary>
public class ServiceCatalogService(AppDbContext db)
{
    public Task<List<Service>> ListAsync(bool includeInactive) =>
        db.Services
            .Where(s => includeInactive || s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();

    public Task<Service?> FindAsync(int id) => db.Services.SingleOrDefaultAsync(s => s.Id == id);

    public async Task<ServiceResult<Service>> CreateAsync(ServiceFormModel form)
    {
        if (await NameTakenAsync(form.Name, exceptId: null))
            return ServiceResult<Service>.Fail(DuplicateName(form.Name));

        var service = new Service { Name = form.Name.Trim() };
        Apply(service, form);
        db.Services.Add(service);
        await db.SaveChangesAsync();
        return ServiceResult<Service>.Ok(service);
    }

    public async Task<ServiceResult> UpdateAsync(int id, ServiceFormModel form)
    {
        var service = await FindAsync(id);
        if (service is null)
            return ServiceResult.Fail("Hizmet bulunamadı.");
        if (await NameTakenAsync(form.Name, exceptId: id))
            return ServiceResult.Fail(DuplicateName(form.Name));

        Apply(service, form);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var service = await FindAsync(id);
        if (service is null)
            return ServiceResult.Fail("Hizmet bulunamadı.");

        // Randevu geçmişi korunur: kullanılmış hizmet silinmez, pasif yapılır.
        if (await db.Appointments.AnyAsync(a => a.ServiceId == id))
            return ServiceResult.Fail("Bu hizmete ait randevular olduğu için silinemez. Bunun yerine pasif hale getirebilirsiniz.");

        db.Services.Remove(service);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private Task<bool> NameTakenAsync(string name, int? exceptId)
    {
        // Türkçe harmanlama ile karşılaştırılır: "IŞIN" = "Işın", "İÇ" = "iç".
        var trimmed = name.Trim();
        return db.Services.AnyAsync(s => s.Id != exceptId && EF.Functions.Collate(s.Name, "Turkish_CI_AS") == trimmed);
    }

    private static string DuplicateName(string name) => $"'{name.Trim()}' adında bir hizmet zaten var.";

    private static void Apply(Service service, ServiceFormModel form)
    {
        service.Name = form.Name.Trim();
        service.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();
        service.DurationMinutes = form.DurationMinutes;
        service.Price = form.Price;
        service.IsActive = form.IsActive;
    }
}
