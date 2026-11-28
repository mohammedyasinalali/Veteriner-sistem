using VetKlinik.Web.Services;

namespace VetKlinik.Web.Api.Dtos;

public record ServiceReportRowDto(int ServiceId, string ServiceName, int AppointmentCount, int CompletedCount, decimal Revenue);

public record ServiceReportDto(
    DateOnly From, DateOnly To, int TotalAppointments, decimal TotalRevenue, IReadOnlyList<ServiceReportRowDto> Services)
{
    public static ServiceReportDto Of(ServiceReport r) => new(
        r.From,
        r.To,
        r.TotalAppointments,
        r.TotalRevenue,
        r.Rows.Select(x => new ServiceReportRowDto(x.ServiceId, x.ServiceName, x.AppointmentCount, x.CompletedCount, x.Revenue)).ToList());
}
