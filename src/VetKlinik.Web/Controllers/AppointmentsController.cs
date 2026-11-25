using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Infrastructure;
using VetKlinik.Web.Models.Appointments;
using VetKlinik.Web.Services;

namespace VetKlinik.Web.Controllers;

/// <summary>Randevularım listesi ve randevu alma. Kurallar AppointmentService'te.</summary>
[Authorize]
public class AppointmentsController(
    AppointmentService appointments, PetService pets, ServiceCatalogService catalog, TimeProvider time) : Controller
{
    private CurrentUser Me => CurrentUser.From(User);

    public async Task<IActionResult> Index() => View(await appointments.ListAsync(Me));

    public async Task<IActionResult> Details(int id)
    {
        var appointment = await appointments.FindAsync(Me, id);
        return appointment is null ? NotFound() : View(appointment);
    }

    // Durum değişiklikleri: yetki ve kurallar AppointmentService'te. Yetkisizse 404,
    // kural ihlalinde Türkçe mesajla geldiği sayfaya döner.

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? returnUrl) =>
        Done(await appointments.ApproveAsync(Me, id), "Randevu onaylandı.", id, returnUrl);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? reason, string? returnUrl) =>
        Done(await appointments.RejectAsync(Me, id, reason), "Randevu reddedildi.", id, returnUrl);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, string? note, string? returnUrl) =>
        Done(await appointments.CompleteAsync(Me, id, note), "Randevu tamamlandı, muayene notu kaydedildi.", id, returnUrl);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? returnUrl) =>
        Done(await appointments.CancelAsync(Me, id), "Randevu iptal edildi.", id, returnUrl);

    private IActionResult Done(ServiceResult result, string success, int id, string? returnUrl)
    {
        if (result.IsNotFound)
            return NotFound();

        if (result.Succeeded)
            TempData["Success"] = success;
        else
            TempData["Error"] = string.Join(" ", result.Errors);

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Hayvan + hizmet + gün seçilince hekimlerin uygun saatlerini listeler.</summary>
    [HttpGet, Authorize(Roles = Roles.Uye)]
    public async Task<IActionResult> Book(int? petId, int? serviceId, DateOnly? date)
    {
        // Başkasının hayvan id'si adres satırına yazılırsa 404 (kaydın varlığı da gizlenir).
        if (petId is not null && await pets.FindEditableAsync(Me, petId.Value) is null)
            return NotFound();

        var searched = petId is not null && serviceId is not null && date is not null;
        return View(new BookingPageModel
        {
            Pets = await pets.ListAsync(Me),
            Services = await catalog.ListAsync(includeInactive: false),
            PetId = petId,
            ServiceId = serviceId,
            Date = date,
            Today = ClinicTime.Today(time),
            Results = searched ? await appointments.GetAvailableSlotsAsync(serviceId!.Value, date!.Value) : null,
        });
    }

    [HttpPost, Authorize(Roles = Roles.Uye), ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(BookingFormModel form)
    {
        if (await pets.FindEditableAsync(Me, form.PetId) is null)
            return NotFound();

        var back = new { petId = form.PetId, serviceId = form.ServiceId, date = form.StartsAt?.ToString("yyyy-MM-dd") };
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Book), back);
        }

        var result = await appointments.BookAsync(Me, form);
        if (!result.Succeeded)
        {
            // Saat listesi güncellensin diye aynı aramaya geri dönülür.
            TempData["Error"] = string.Join(" ", result.Errors);
            return RedirectToAction(nameof(Book), back);
        }

        var a = result.Value!;
        TempData["Success"] = $"Randevu talebiniz alındı: {a.StartsAt:dd.MM.yyyy HH:mm}. Hekim onayladığında durum \"Onaylandı\" olacak.";
        return RedirectToAction(nameof(Index));
    }
}
