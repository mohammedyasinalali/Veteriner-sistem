using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Domain;
using VetKlinik.Web.Models.Pets;
using VetKlinik.Web.Services;

namespace VetKlinik.Web.Controllers;

/// <summary>
/// Evcil hayvan ekranları. Yetki kararını PetService verir: kullanıcının göremediği
/// veya düzenleyemediği hayvan için 404 döner (kaydın varlığı da belli olmaz).
/// </summary>
[Authorize]
public class PetsController(PetService pets, PhotoStorage photos) : Controller
{
    private const long MaxRequestBytes = 3 * 1024 * 1024;

    private CurrentUser Me => CurrentUser.From(User);

    public async Task<IActionResult> Index() => View(await pets.ListAsync(Me));

    public async Task<IActionResult> Details(int id)
    {
        var pet = await pets.FindAsync(Me, id);
        if (pet is null)
            return NotFound();

        ViewData["CanEdit"] = await pets.FindEditableAsync(Me, id) is not null;
        return View(pet);
    }

    /// <summary>Fotoğraf yalnızca hayvanı görebilen kullanıcıya gönderilir.</summary>
    public async Task<IActionResult> Photo(int id)
    {
        var pet = await pets.FindAsync(Me, id);
        var path = photos.PathOf(pet?.PhotoFileName);
        if (path is null)
            return NotFound();
        // Yetkiye bağlı içerik: ortak önbelleklerde (proxy) saklanmasın.
        Response.Headers.CacheControl = "private, max-age=300";
        return PhysicalFile(path, path.EndsWith(".png") ? "image/png" : "image/jpeg");
    }

    [HttpGet, Authorize(Roles = Roles.Uye)]
    public IActionResult Create() => View(new PetFormModel { BirthDate = DateOnly.FromDateTime(DateTime.Today.AddYears(-1)) });

    [HttpPost, Authorize(Roles = Roles.Uye), ValidateAntiForgeryToken, RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Create(PetFormModel form)
    {
        if (!ModelState.IsValid)
            return View(form);

        var result = await pets.CreateAsync(Me, form);
        if (!result.Succeeded)
            return ViewWithErrors(nameof(Create), form, result);

        TempData["Success"] = $"{result.Value!.Name} eklendi.";
        return RedirectToAction(nameof(Details), new { id = result.Value.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var pet = await pets.FindEditableAsync(Me, id);
        if (pet is null)
            return NotFound();

        ViewData["Pet"] = pet;
        return View(ToForm(pet));
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Edit(int id, PetFormModel form)
    {
        var pet = await pets.FindEditableAsync(Me, id);
        if (pet is null)
            return NotFound();

        ViewData["Pet"] = pet;
        if (!ModelState.IsValid)
            return View(form);

        var result = await pets.UpdateAsync(Me, id, form);
        if (!result.Succeeded)
            return ViewWithErrors(nameof(Edit), form, result);

        TempData["Success"] = $"{form.Name.Trim()} güncellendi.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var pet = await pets.FindEditableAsync(Me, id);
        return pet is null ? NotFound() : View(pet);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var pet = await pets.FindEditableAsync(Me, id);
        if (pet is null)
            return NotFound();

        var result = await pets.DeleteAsync(Me, id);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error);
            return View("Delete", pet);
        }

        TempData["Success"] = $"{pet.Name} silindi.";
        return RedirectToAction(nameof(Index));
    }

    private IActionResult ViewWithErrors(string view, PetFormModel form, ServiceResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error);
        return View(view, form);
    }

    private static PetFormModel ToForm(Pet p) => new()
    {
        Name = p.Name,
        Species = p.Species,
        Breed = p.Breed,
        BirthDate = p.BirthDate,
        WeightKg = p.WeightKg,
    };
}
