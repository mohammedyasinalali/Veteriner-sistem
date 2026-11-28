using VetKlinik.Tests.Infrastructure;
using VetKlinik.Web.Domain;
using VetKlinik.Web.Services;

namespace VetKlinik.Tests.Services;

public class ReportServiceTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private async Task AddAsync(Seeded seed, Service service, DateTime startsAt, AppointmentStatus status, decimal? price = null)
    {
        await using var db = fixture.NewContext();
        var a = seed.NewAppointment(startsAt);
        a.ServiceId = service.Id;
        a.EndsAt = startsAt.AddMinutes(service.DurationMinutes);
        a.DurationMinutes = service.DurationMinutes;
        a.Price = price ?? service.Price;
        a.Status = status;
        db.Appointments.Add(a);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Groups_by_service_and_counts_only_completed_revenue()
    {
        var seed = await fixture.ResetAsync();
        var day = new DateTime(2026, 11, 10, 10, 0, 0);
        await AddAsync(seed, seed.Examination, day, AppointmentStatus.Completed);
        await AddAsync(seed, seed.Examination, day.AddHours(1), AppointmentStatus.Completed, price: 450m); // eski fiyat
        await AddAsync(seed, seed.Examination, day.AddHours(2), AppointmentStatus.Approved);
        await AddAsync(seed, seed.Examination, day.AddHours(3), AppointmentStatus.Cancelled);
        await AddAsync(seed, seed.Examination, day.AddHours(4), AppointmentStatus.Rejected);
        await AddAsync(seed, seed.Neutering, day.AddDays(1), AppointmentStatus.Completed);
        await using var db = fixture.NewContext();

        var report = await new ReportService(db).ServicesAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

        Assert.Equal(4, report.TotalAppointments);
        Assert.Equal(3950m, report.TotalRevenue);
        Assert.Collection(report.Rows,
            neutering =>
            {
                Assert.Equal("Kısırlaştırma", neutering.ServiceName);
                Assert.Equal(1, neutering.AppointmentCount);
                Assert.Equal(3000m, neutering.Revenue);
            },
            exam =>
            {
                Assert.Equal(seed.Examination.Id, exam.ServiceId);
                Assert.Equal(3, exam.AppointmentCount); // iptal ve ret sayılmaz
                Assert.Equal(2, exam.CompletedCount);
                Assert.Equal(950m, exam.Revenue);
            });
    }

    [Fact]
    public async Task Range_includes_both_ends_and_excludes_outside()
    {
        var seed = await fixture.ResetAsync();
        await AddAsync(seed, seed.Examination, new DateTime(2026, 10, 31, 17, 30, 0), AppointmentStatus.Completed);
        await AddAsync(seed, seed.Examination, new DateTime(2026, 11, 1, 9, 0, 0), AppointmentStatus.Completed);
        await AddAsync(seed, seed.Examination, new DateTime(2026, 11, 30, 17, 30, 0), AppointmentStatus.Completed);
        await AddAsync(seed, seed.Examination, new DateTime(2026, 12, 1, 9, 0, 0), AppointmentStatus.Completed);
        await using var db = fixture.NewContext();

        var report = await new ReportService(db).ServicesAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

        var only = Assert.Single(report.Rows);
        Assert.Equal(2, only.CompletedCount);
    }

    [Fact]
    public async Task Service_with_only_cancelled_appointments_shows_zero()
    {
        var seed = await fixture.ResetAsync();
        await AddAsync(seed, seed.Neutering, new DateTime(2026, 11, 5, 10, 0, 0), AppointmentStatus.Cancelled);
        await using var db = fixture.NewContext();

        var report = await new ReportService(db).ServicesAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

        var row = Assert.Single(report.Rows);
        Assert.Equal(0, row.AppointmentCount);
        Assert.Equal(0m, row.Revenue);
        Assert.Equal(0, report.TotalAppointments);
    }
}
