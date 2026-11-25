using Microsoft.EntityFrameworkCore;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Data;
using VetKlinik.Web.Domain;
using VetKlinik.Web.Models.Pets;

namespace VetKlinik.Web.Services;

/// <summary>
/// Evcil hayvan iş kuralları ve sahiplik yetkisi. Her sorgu <see cref="Visible"/> üzerinden yapılır;
/// kullanıcının göremediği kayıt "yok" gibi davranır (controller 404 döner).
/// </summary>
public class PetService(AppDbContext db, PhotoStorage photos, TimeProvider time)
{
    private const string NotFound = "Hayvan bulunamadı.";

    /// <summary>Admin: hepsi. Hekim: kendisinden randevusu olanlar. Üye: yalnızca kendi hayvanları.</summary>
    private IQueryable<Pet> Visible(CurrentUser user)
    {
        if (user.IsAdmin)
            return db.Pets;
        if (user.IsVeterinarian)
            return db.Pets.Where(p => p.Appointments.Any(a => a.Veterinarian.UserId == user.Id));
        return db.Pets.Where(p => p.OwnerId == user.Id);
    }

    /// <summary>Düzenleme/silme yetkisi: yalnızca sahibi veya Admin.</summary>
    private IQueryable<Pet> Editable(CurrentUser user) =>
        user.IsAdmin ? db.Pets : db.Pets.Where(p => p.OwnerId == user.Id);

    public Task<List<Pet>> ListAsync(CurrentUser user) =>
        Visible(user).Include(p => p.Owner).OrderBy(p => p.Name).ToListAsync();

    public Task<Pet?> FindAsync(CurrentUser user, int id) =>
        Visible(user).Include(p => p.Owner).SingleOrDefaultAsync(p => p.Id == id);

    public Task<Pet?> FindEditableAsync(CurrentUser user, int id) =>
        Editable(user).Include(p => p.Owner).SingleOrDefaultAsync(p => p.Id == id);

    public async Task<ServiceResult<Pet>> CreateAsync(CurrentUser user, PetFormModel form)
    {
        var errors = await ValidateAsync(user.Id, form, exceptId: null);
        if (errors.Length > 0)
            return ServiceResult<Pet>.Fail(errors);

        var pet = new Pet { OwnerId = user.Id, Name = "", Breed = "" };
        Apply(pet, form);
        if (form.Photo is not null)
            pet.PhotoFileName = await photos.SaveAsync(form.Photo);

        db.Pets.Add(pet);
        await db.SaveChangesAsync();
        return ServiceResult<Pet>.Ok(pet);
    }

    public async Task<ServiceResult> UpdateAsync(CurrentUser user, int id, PetFormModel form)
    {
        var pet = await FindEditableAsync(user, id);
        if (pet is null)
            return ServiceResult.Fail(NotFound);

        // Ad benzersizliği hayvanın gerçek sahibine göre kontrol edilir (Admin düzenlese bile).
        var errors = await ValidateAsync(pet.OwnerId, form, exceptId: id);
        if (errors.Length > 0)
            return ServiceResult.Fail(errors);

        Apply(pet, form);
        var oldPhoto = pet.PhotoFileName;
        if (form.Photo is not null)
            pet.PhotoFileName = await photos.SaveAsync(form.Photo);
        else if (form.RemovePhoto)
            pet.PhotoFileName = null;

        await db.SaveChangesAsync();
        if (oldPhoto != pet.PhotoFileName)
            photos.Delete(oldPhoto);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(CurrentUser user, int id)
    {
        var pet = await FindEditableAsync(user, id);
        if (pet is null)
            return ServiceResult.Fail(NotFound);

        // Randevu geçmişi korunur; aşı kayıtları hayvanla birlikte silinir (cascade).
        if (await db.Appointments.AnyAsync(a => a.PetId == id))
            return ServiceResult.Fail("Bu hayvana ait randevular olduğu için silinemez.");

        db.Pets.Remove(pet);
        await db.SaveChangesAsync();
        photos.Delete(pet.PhotoFileName);
        return ServiceResult.Ok();
    }

    private async Task<string[]> ValidateAsync(string ownerId, PetFormModel form, int? exceptId)
    {
        var errors = new List<string>();
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        if (form.BirthDate > today)
            errors.Add("Doğum tarihi gelecekte olamaz.");

        // Aynı sahibin iki hayvanı aynı adı taşıyamaz (MCP araçları hayvanı adıyla bulur).
        var name = form.Name.Trim();
        if (await db.Pets.AnyAsync(p => p.OwnerId == ownerId && p.Id != exceptId
                && EF.Functions.Collate(p.Name, "Turkish_CI_AS") == name))
            errors.Add($"'{name}' adında bir hayvanınız zaten var.");

        if (form.Photo is not null)
            errors.AddRange(photos.Validate(form.Photo).Errors);
        return [.. errors];
    }

    private static void Apply(Pet pet, PetFormModel form)
    {
        pet.Name = form.Name.Trim();
        pet.Species = form.Species;
        pet.Breed = form.Breed.Trim();
        pet.BirthDate = form.BirthDate;
        pet.WeightKg = form.WeightKg;
    }
}
