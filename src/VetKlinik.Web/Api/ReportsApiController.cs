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
    /// <remarks>
    /// Yalnız biri verilirse diğeri aynı ayın başı/sonu olur. Saati geçmiş ama yanıtlanmamış (onay bekleyen)
    /// talepler de "randevu" sayılır: talep gelmiştir; gelire yalnız tamamlananlar girer.
    /// </remarks>
    [HttpGet("services")]
    public async Task<ActionResult<ServiceReportDto>> Services(
        [ModelBinder(typeof(IsoDateBinder))] DateOnly? from, [ModelBinder(typeof(IsoDateBinder))] DateOnly? to)
    {
        if (from == DateOnly.MaxValue || to == DateOnly.MaxValue)
            return Error(StatusCodes.Status400BadRequest, "Tarih aralığı geçersiz.", ["Tarih geçersiz."]);

        var month = from ?? to ?? ClinicTime.Today(time);
        var start = from ?? new DateOnly(month.Year, month.Month, 1);
        var end = to ?? new DateOnly(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));

        if (start > end)
            return Error(StatusCodes.Status400BadRequest, "Tarih aralığı geçersiz.", ["Başlangıç tarihi bitiş tarihinden sonra olamaz."]);
        if (end.DayNumber - start.DayNumber >= MaxRangeDays)
            return Error(StatusCodes.Status400BadRequest, "Tarih aralığı geçersiz.", ["Aralık en fazla bir yıl olabilir."]);

        return ServiceReportDto.Of(await reports.ServicesAsync(start, end));
    }
}
