using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Services;

namespace VetKlinik.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class SpecialtiesController(SpecialtyService specialties) : Controller
{
    public async Task<IActionResult> Index() => View(await specialties.ListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name)
    {
        var result = await specialties.CreateAsync(name ?? "");
        if (result.Succeeded)
            TempData["Success"] = $"'{name!.Trim()}' uzmanlık alanı eklendi.";
        else
            TempData["Error"] = string.Join(" ", result.Errors);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await specialties.DeleteAsync(id);
        if (result.Succeeded)
            TempData["Success"] = "Uzmanlık alanı silindi.";
        else
            TempData["Error"] = string.Join(" ", result.Errors);
        return RedirectToAction(nameof(Index));
    }
}
