using Microsoft.EntityFrameworkCore;
using VetKlinik.Web.Data;
using VetKlinik.Web.Domain;

namespace VetKlinik.Web.Services;

/// <summary>Bir hizmetin seçilen aralıktaki randevu sayısı ve geliri.</summary>
public record ServiceReportRow(int ServiceId, string ServiceName, int AppointmentCount, int CompletedCount, decimal Revenue);

public record ServiceReport(DateOnly From, DateOnly To, IReadOnlyList<ServiceReportRow> Rows)
{
    public int TotalAppointments => Rows.Sum(r => r.AppointmentCount);
    public decimal TotalRevenue => Rows.Sum(r => r.Revenue);
}

/// <summary>Yönetici raporları. Gruplama ve toplamlar SQL'de yapılır (LINQ GroupBy → GROUP BY).</summary>
public class ReportService(AppDbContext db)
{
    /// <summary>
    /// [from, to] günleri arasında başlayan randevular, hizmete göre gruplanır.
    /// İptal ve ret sayılmaz; gelir yalnızca tamamlanan randevuların o anki ücretidir.
    /// </summary>
    public async Task<ServiceReport> ServicesAsync(DateOnly from, DateOnly to)
    {
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var groups = await db.Appointments
            .Where(a => a.StartsAt >= start && a.StartsAt < end)
            .GroupBy(a => new { a.ServiceId, a.Service.Name })
            .Select(g => new
            {
                g.Key.ServiceId,
                g.Key.Name,
                Count = g.Count(a => a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Rejected),
                Completed = g.Count(a => a.Status == AppointmentStatus.Completed),
                Revenue = g.Sum(a => a.Status == AppointmentStatus.Completed ? a.Price : 0m),
            })
            .OrderByDescending(r => r.Revenue)
            .ThenByDescending(r => r.Count)
            .ThenBy(r => r.Name)
            .ToListAsync();

        var rows = groups.Select(g => new ServiceReportRow(g.ServiceId, g.Name, g.Count, g.Completed, g.Revenue)).ToList();
        return new ServiceReport(from, to, rows);
    }
}
