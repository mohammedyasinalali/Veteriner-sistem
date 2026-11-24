using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VetKlinik.Web.Models;
using VetKlinik.Web.Services;

namespace VetKlinik.Web.Controllers;

public class HomeController(ServiceCatalogService catalog, ClinicService clinics) : Controller
{
    public async Task<IActionResult> Index() =>
        View(new HomeViewModel(await clinics.GetAsync(), await catalog.ListAsync(includeInactive: false)));

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
