using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Infrastructure;
using VetKlinik.Web.Models.Pets;
using VetKlinik.Web.Services;

namespace VetKlinik.Web.Controllers;

/// <summary>Aşı kaydı ekranları. Yetkisiz hayvan/kayıt için 404 (karar VaccineService ve PetService'te).</summary>
[Authorize]
public class VaccinesController(VaccineService vaccines, PetService pets, TimeProvider time) : Controller
{
    private CurrentUser Me => CurrentUser.From(User);

    [HttpGet]
    public async Task<IActionResult> Create(int petId)
    {
        var pet = await pets.FindAsync(Me, petId);
        if (pet is null)
            return NotFound();

        ViewData["Pet"] = pet;
        return View(new VaccineFormModel { AdministeredOn = ClinicTime.Today(time) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int petId, VaccineFormModel form)
    {
        var pet = await pets.FindAsync(Me, petId);
        if (pet is null)
            return NotFound();

        ViewData["Pet"] = pet;
        if (!ModelState.IsValid)
            return View(form);

        var result = await vaccines.AddAsync(Me, petId, form);
        if (!result.Succeeded)
            return ViewWithErrors(form, result);

        TempData["Success"] = $"{form.VaccineName.Trim()} aşısı kaydedildi.";
        return BackToPet(petId);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var record = await vaccines.FindEditableAsync(Me, id);
        if (record is null)
            return NotFound();

        ViewData["Record"] = record;
        return View(new VaccineFormModel
        {
            VaccineName = record.VaccineName,
            AdministeredOn = record.AdministeredOn,
            NextDueOn = record.NextDueOn,
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, VaccineFormModel form)
    {
        var record = await vaccines.FindEditableAsync(Me, id);
        if (record is null)
            return NotFound();

        ViewData["Record"] = record;
        if (!ModelState.IsValid)
            return View(form);

        var result = await vaccines.UpdateAsync(Me, id, form);
        if (!result.Succeeded)
            return ViewWithErrors(form, result);

        TempData["Success"] = "Aşı kaydı güncellendi.";
        return BackToPet(record.PetId);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var record = await vaccines.FindEditableAsync(Me, id);
        return record is null ? NotFound() : View(record);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var record = await vaccines.FindEditableAsync(Me, id);
        if (record is null)
            return NotFound();

        await vaccines.DeleteAsync(Me, id);
        TempData["Success"] = $"{record.VaccineName} kaydı silindi.";
        return BackToPet(record.PetId);
    }

    private IActionResult BackToPet(int petId) =>
        Redirect(Url.Action("Details", "Pets", new { id = petId }) + "#asilar");

    private IActionResult ViewWithErrors(VaccineFormModel form, ServiceResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error);
        return View(form);
    }
}
