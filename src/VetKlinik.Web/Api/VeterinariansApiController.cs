using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using VetKlinik.Web.Api.Dtos;
using VetKlinik.Web.Services;

namespace VetKlinik.Web.Api;

[Route("api/veterinarians")]
public class VeterinariansApiController(
    VeterinarianService veterinarians, ServiceCatalogService catalog, AppointmentService appointments) : ApiControllerBase
{
    /// <summary>Aktif hekimler, uzmanlıkları ve verdikleri aktif hizmetler.</summary>
    [HttpGet]
    public async Task<ActionResult<List<VeterinarianDto>>> List() =>
        (await veterinarians.ListAsync())
            .Where(v => v.IsActive)
            .Select(VeterinarianDto.From)
            .ToList();

    /// <summary>Hizmet ve gün için hekimlerin uygun başlangıç saatleri (randevu kaydıyla aynı kural).</summary>
    [HttpGet("available")]
    public async Task<ActionResult<AvailabilityDto>> Available(
        [Required(ErrorMessage = "Hizmet (serviceId) zorunludur.")] int? serviceId,
        [Required(ErrorMessage = "Tarih (date, yyyy-MM-dd) zorunludur.")] DateOnly? date,
        int? veterinarianId)
    {
        var service = await catalog.FindAsync(serviceId!.Value);
        if (service is null || !service.IsActive)
            return NotFoundError("Hizmet bulunamadı veya şu an verilmiyor.");

        var slots = await appointments.GetAvailableSlotsAsync(service.Id, date!.Value, veterinarianId);
        return new AvailabilityDto(service.Id, service.Name, service.DurationMinutes, date.Value,
            slots.Select(VetSlotsDto.From).ToList());
    }
}
