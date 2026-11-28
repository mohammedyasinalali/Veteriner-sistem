using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetKlinik.Web.Api.Dtos;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Infrastructure;
using VetKlinik.Web.Services;

namespace VetKlinik.Web.Api;

[Route("api/reports")]
[Authorize(Roles = Roles.Admin)]
public class ReportsApiController(ReportService reports, TimeProvider time) : ApiControllerBase
{
    public const int MaxRangeDays = 366;

    /// <summary>Hizmet başına randevu sayısı ve gelir. Tarih verilmezse içinde bulunulan ay.</summary>
    [HttpGet("services")]
    public async Task<ActionResult<ServiceReportDto>> Services(DateOnly? from, DateOnly? to)
    {
        var today = ClinicTime.Today(time);
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

        if (start > end)
            return Error(StatusCodes.Status400BadRequest, "Tarih aralığı geçersiz.", ["Başlangıç tarihi bitiş tarihinden sonra olamaz."]);
        if (end.DayNumber - start.DayNumber >= MaxRangeDays || end == DateOnly.MaxValue)
            return Error(StatusCodes.Status400BadRequest, "Tarih aralığı geçersiz.", ["Aralık en fazla bir yıl olabilir."]);

        return ServiceReportDto.Of(await reports.ServicesAsync(start, end));
    }
}
