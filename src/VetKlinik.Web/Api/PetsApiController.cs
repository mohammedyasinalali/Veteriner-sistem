using Microsoft.AspNetCore.Mvc;
using VetKlinik.Web.Api.Dtos;
using VetKlinik.Web.Services;

namespace VetKlinik.Web.Api;

[Route("api/pets")]
public class PetsApiController(VaccineService vaccines) : ApiControllerBase
{
    /// <summary>Aşı geçmişi ve yaklaşan dozlar. Hayvanı göremeyen kullanıcı için 404.</summary>
    [HttpGet("{id:int}/vaccines")]
    public async Task<ActionResult<PetVaccinesDto>> Vaccines(int id)
    {
        var schedule = await vaccines.GetScheduleAsync(Me, id);
        return schedule is null ? NotFoundError("Hayvan bulunamadı.") : PetVaccinesDto.From(schedule);
    }
}
