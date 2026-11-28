using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using VetKlinik.Web.Api.Dtos;
using VetKlinik.Web.Infrastructure;
using VetKlinik.Web.Services;

namespace VetKlinik.Web.Api;

[Route("api/veterinarians")]
public class VeterinariansApiController(
    VeterinarianService veterinarians, ServiceCatalogService catalog, AppointmentService appointments, TimeProvider time) : ApiControllerBase
{
    /// <summary>Aktif hekimler, uzmanlıkları ve verdikleri aktif hizmetler.</summary>
    [HttpGet]
    public async Task<ActionResult<List<VeterinarianDto>>> List() =>
        (await veterinarians.ListAsync(activeOnly: true))
            .Select(VeterinarianDto.From)
            .ToList();

    /// <summary>Hizmet ve gün için hekimlerin uygun başlangıç saatleri (randevu kaydıyla aynı kural).</summary>
    [HttpGet("available")]
    public async Task<ActionResult<AvailabilityDto>> Available(
        [Required(ErrorMessage = "Hizmet (serviceId) zorunludur.")] int? serviceId,
        [Required(ErrorMessage = "Tarih (date, yyyy-MM-dd) zorunludur."), ModelBinder(typeof(IsoDateBinder))] DateOnly? date,
        int? veterinarianId)
    {
        // Randevu kaydının vereceği nedenler burada da söylenir; boş liste "o gün yer yok" demektir.
        var today = ClinicTime.Today(time);
        if (date < today)
            return BadRequestError("Seçilen gün geçmişte kaldı; bugün veya sonrası için arayın.");
        if (date > today.AddDays(AppointmentService.MaxDaysAhead))
            return BadRequestError($"En fazla {AppointmentService.MaxDaysAhead} gün sonrası için randevu alınabilir.");

        var service = await catalog.FindAsync(serviceId!.Value);
        if (service is null || !service.IsActive)
            return NotFoundError("Hizmet bulunamadı veya şu an verilmiyor.");
        if (veterinarianId is not null && await veterinarians.FindAsync(veterinarianId.Value) is not { IsActive: true })
            return NotFoundError("Hekim bulunamadı.");

        var slots = await appointments.GetAvailableSlotsAsync(service.Id, date!.Value, veterinarianId);
        return new AvailabilityDto(service.Id, service.Name, service.DurationMinutes, date.Value,
            slots.Select(VetSlotsDto.From).ToList());
    }
}
