using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Models.Admin;
using VetKlinik.Web.Services;

namespace VetKlinik.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class ClinicController(ClinicService clinics) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Edit() => View(await clinics.GetFormAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ClinicFormModel form)
    {
        if (!ModelState.IsValid)
            return View(form);

        var result = await clinics.UpdateAsync(form);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error);
            return View(form);
        }

        TempData["Success"] = "Klinik bilgileri ve çalışma saatleri kaydedildi.";
        return RedirectToAction(nameof(Edit));
    }
}
