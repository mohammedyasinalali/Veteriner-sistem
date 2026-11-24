using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Domain;
using VetKlinik.Web.Models.Admin;
using VetKlinik.Web.Services;

namespace VetKlinik.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class ServicesController(ServiceCatalogService catalog) : Controller
{
    public async Task<IActionResult> Index() => View(await catalog.ListAsync(includeInactive: true));

    [HttpGet]
    public IActionResult Create() => View(new ServiceFormModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceFormModel form)
    {
        if (!ModelState.IsValid)
            return View(form);

        var result = await catalog.CreateAsync(form);
        if (!result.Succeeded)
            return ViewWithErrors(form, result);

        TempData["Success"] = $"'{form.Name}' hizmeti eklendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var service = await catalog.FindAsync(id);
        if (service is null)
            return NotFound();

        ViewData["Id"] = id;
        return View(ToForm(service));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ServiceFormModel form)
    {
        if (await catalog.FindAsync(id) is null)
            return NotFound();

        ViewData["Id"] = id;
        if (!ModelState.IsValid)
            return View(form);

        var result = await catalog.UpdateAsync(id, form);
        if (!result.Succeeded)
            return ViewWithErrors(form, result);

        TempData["Success"] = $"'{form.Name}' hizmeti güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var service = await catalog.FindAsync(id);
        return service is null ? NotFound() : View(service);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var service = await catalog.FindAsync(id);
        if (service is null)
            return NotFound();

        var result = await catalog.DeleteAsync(id);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error);
            return View("Delete", service);
        }

        TempData["Success"] = $"'{service.Name}' hizmeti silindi.";
        return RedirectToAction(nameof(Index));
    }

    private IActionResult ViewWithErrors(ServiceFormModel form, ServiceResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error);
        return View(form);
    }

    private static ServiceFormModel ToForm(Service s) => new()
    {
        Name = s.Name,
        Description = s.Description,
        DurationMinutes = s.DurationMinutes,
        Price = s.Price,
        IsActive = s.IsActive,
    };
}
