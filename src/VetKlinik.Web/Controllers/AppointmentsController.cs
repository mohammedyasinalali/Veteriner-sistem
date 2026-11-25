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

        var back = new { petId = form.PetId, serviceId = form.ServiceId, date = DateOnly.FromDateTime(form.StartsAt).ToString("yyyy-MM-dd") };
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
