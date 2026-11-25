using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Services;

namespace VetKlinik.Web.Controllers;

/// <summary>Hekim paneli: onay bekleyen, bugünkü, yaklaşan ve tamamlanmayı bekleyen randevular.</summary>
[Authorize(Roles = Roles.Veteriner), Route("Hekim")]
public class VetPanelController(AppointmentService appointments) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index() => View(await appointments.GetVetDashboardAsync(CurrentUser.From(User)));
}
