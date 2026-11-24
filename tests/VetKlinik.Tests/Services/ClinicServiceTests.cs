using Microsoft.EntityFrameworkCore;
using VetKlinik.Tests.Infrastructure;
using VetKlinik.Web.Services;

namespace VetKlinik.Tests.Services;

public class ClinicServiceTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task Form_starts_on_monday()
    {
        await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var form = await new ClinicService(db).GetFormAsync();

        Assert.Equal(7, form.Days.Count);
        Assert.Equal(DayOfWeek.Monday, form.Days[0].Day);
        Assert.Equal(DayOfWeek.Sunday, form.Days[6].Day);
    }

    [Fact]
    public async Task Update_saves_hours_and_info()
    {
        await fixture.ResetAsync();
        await using var db = fixture.NewContext();
        var service = new ClinicService(db);
        var form = await service.GetFormAsync();
        form.Phone = "0264 111 22 33";
        form.Days[5].IsClosed = false; // Cumartesi
        form.Days[5].Opens = new TimeOnly(10, 0);
        form.Days[5].Closes = new TimeOnly(14, 0);

        var result = await service.UpdateAsync(form);

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        await using var fresh = fixture.NewContext();
        var clinic = await fresh.Clinics.Include(c => c.Hours).SingleAsync();
        Assert.Equal("0264 111 22 33", clinic.Phone);
        var saturday = clinic.Hours.Single(h => h.Day == DayOfWeek.Saturday);
        Assert.False(saturday.IsClosed);
        Assert.Equal(new TimeOnly(14, 0), saturday.Closes);
    }

    [Fact]
    public async Task Update_rejects_closing_before_opening()
    {
        await fixture.ResetAsync();
        await using var db = fixture.NewContext();
        var service = new ClinicService(db);
        var form = await service.GetFormAsync();
        form.Days[0].Opens = new TimeOnly(18, 0);
        form.Days[0].Closes = new TimeOnly(9, 0);

        var result = await service.UpdateAsync(form);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("Pazartesi"));
    }

    [Fact]
    public async Task Closed_day_hours_are_not_validated()
    {
        await fixture.ResetAsync();
        await using var db = fixture.NewContext();
        var service = new ClinicService(db);
        var form = await service.GetFormAsync();
        form.Days[6].IsClosed = true;
        form.Days[6].Opens = TimeOnly.MinValue;
        form.Days[6].Closes = TimeOnly.MinValue;

        var result = await service.UpdateAsync(form);

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
    }
}
