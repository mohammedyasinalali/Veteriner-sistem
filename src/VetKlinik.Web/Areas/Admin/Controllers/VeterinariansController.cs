using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Models.Admin;
using VetKlinik.Web.Services;

namespace VetKlinik.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class VeterinariansController(
    VeterinarianService veterinarians,
    SpecialtyService specialties,
    ServiceCatalogService catalog) : Controller
{
    public async Task<IActionResult> Index() => View(await veterinarians.ListAsync());

    [HttpGet]
    public async Task<IActionResult> Create() => await FormView(await veterinarians.NewFormAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VeterinarianFormModel form)
    {
        if (!ModelState.IsValid)
            return await FormView(form);

        var result = await veterinarians.CreateAsync(form);
        if (!result.Succeeded)
            return await FormView(form, result);

        TempData["Success"] = $"{form.FullName} eklendi. Hekim, e-posta adresi ve belirlediğiniz parola ile giriş yapabilir.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var form = await veterinarians.GetFormAsync(id);
        if (form is null)
            return NotFound();

        ViewData["Id"] = id;
        return await FormView(form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, VeterinarianFormModel form)
    {
        var current = await veterinarians.GetFormAsync(id);
        if (current is null)
            return NotFound();

        ViewData["Id"] = id;
        form.Email = current.Email; // e-posta düzenlenemez
        ModelState.Remove(nameof(form.Email));
        if (!ModelState.IsValid)
            return await FormView(form);

        var result = await veterinarians.UpdateAsync(id, form);
        if (!result.Succeeded)
            return await FormView(form, result);

        TempData["Success"] = $"{form.FullName} güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var vet = await veterinarians.FindAsync(id);
        return vet is null ? NotFound() : View(vet);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var vet = await veterinarians.FindAsync(id);
        if (vet is null)
            return NotFound();

        var result = await veterinarians.DeleteAsync(id);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error);
            return View("Delete", vet);
        }

        TempData["Success"] = $"{vet.User.FullName} silindi.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Formdaki seçim listeleri: tüm uzmanlıklar ve aktif hizmetler (+ hekimde zaten kayıtlı pasifler).</summary>
    private async Task<IActionResult> FormView(VeterinarianFormModel form, ServiceResult? result = null)
    {
        foreach (var error in result?.Errors ?? [])
            ModelState.AddModelError(string.Empty, error);

        ViewData["Specialties"] = await specialties.ListAsync();
        ViewData["Services"] = (await catalog.ListAsync(includeInactive: true))
            .Where(s => s.IsActive || form.ServiceIds.Contains(s.Id))
            .ToList();
        return View(ViewData.ContainsKey("Id") ? "Edit" : "Create", form);
    }
}
