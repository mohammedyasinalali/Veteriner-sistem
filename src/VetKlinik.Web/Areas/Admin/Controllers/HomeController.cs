using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Data;

namespace VetKlinik.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class HomeController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewData["UserCount"] = await db.Users.CountAsync();
        ViewData["VetCount"] = await db.Veterinarians.CountAsync(v => v.IsActive);
        ViewData["ServiceCount"] = await db.Services.CountAsync(s => s.IsActive);
        return View();
    }
}
