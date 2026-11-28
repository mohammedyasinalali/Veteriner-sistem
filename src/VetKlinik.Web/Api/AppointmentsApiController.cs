using Microsoft.AspNetCore.Mvc;
using VetKlinik.Web.Api.Dtos;
using VetKlinik.Web.Services;

namespace VetKlinik.Web.Api;

[Route("api/appointments")]
public class AppointmentsApiController(AppointmentService appointments) : ApiControllerBase
{
    /// <summary>Kullanıcının görebildiği randevular (Üye: hayvanlarının, Hekim: kendisinin, Admin: hepsi).</summary>
    [HttpGet]
    public async Task<ActionResult<List<AppointmentDto>>> List() =>
        (await appointments.ListAsync(Me)).Select(AppointmentDto.From).ToList();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AppointmentDto>> Get(int id)
    {
        var appointment = await appointments.FindAsync(Me, id);
        return appointment is null ? NotFoundError("Randevu bulunamadı.") : AppointmentDto.From(appointment);
    }

    /// <summary>
    /// Randevu oluşturur (durum: onay bekliyor). Kural MVC ile aynıdır: çakışma, mesai, süre, geçmiş saat.
    /// 201 + Location; kural hatası 400; başkasının hayvanı 404.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AppointmentDto>> Create(CreateAppointmentDto? dto)
    {
        if (dto is null)
            return Error(StatusCodes.Status400BadRequest, "İstek geçersiz.", ["İstek gövdesi boş. JSON olarak petId, serviceId, veterinarianId ve startsAt gönderin."]);

        var result = await appointments.BookAsync(Me, dto.ToForm());
        if (!result.Succeeded)
            return Failure(result, "Randevu oluşturulamadı.");

        var saved = (await appointments.FindAsync(Me, result.Value!.Id))!;
        return CreatedAtAction(nameof(Get), new { id = saved.Id }, AppointmentDto.From(saved));
    }
}
