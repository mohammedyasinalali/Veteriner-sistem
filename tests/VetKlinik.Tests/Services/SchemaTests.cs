using Microsoft.EntityFrameworkCore;
using VetKlinik.Tests.Infrastructure;
using VetKlinik.Web.Domain;

namespace VetKlinik.Tests.Services;

public class SchemaTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private static readonly DateTime Monday9 = new(2026, 12, 7, 9, 0, 0);

    [Fact]
    public async Task Deleting_service_with_appointment_is_blocked()
    {
        var seed = await fixture.ResetAsync();
        await using (var db = fixture.NewContext())
        {
            db.Appointments.Add(seed.NewAppointment(Monday9));
            await db.SaveChangesAsync();
        }

        await using var fresh = fixture.NewContext();
        fresh.Services.Remove(await fresh.Services.SingleAsync(s => s.Id == seed.Examination.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => fresh.SaveChangesAsync());
    }

    [Fact]
    public async Task Deleting_pet_with_appointment_is_blocked()
    {
        var seed = await fixture.ResetAsync();
        await using (var db = fixture.NewContext())
        {
            db.Appointments.Add(seed.NewAppointment(Monday9));
            await db.SaveChangesAsync();
        }

        await using var fresh = fixture.NewContext();
        fresh.Pets.Remove(await fresh.Pets.SingleAsync(p => p.Id == seed.PetA.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => fresh.SaveChangesAsync());
    }

    [Fact]
    public async Task Deleting_pet_deletes_its_vaccines()
    {
        var seed = await fixture.ResetAsync();
        await using (var db = fixture.NewContext())
        {
            db.VaccineRecords.Add(new VaccineRecord
            {
                PetId = seed.PetA.Id,
                VaccineName = "Kuduz",
                AdministeredOn = new DateOnly(2026, 1, 10),
            });
            await db.SaveChangesAsync();
        }

        await using var fresh = fixture.NewContext();
        fresh.Pets.Remove(await fresh.Pets.SingleAsync(p => p.Id == seed.PetA.Id));
        await fresh.SaveChangesAsync();

        Assert.Equal(0, await fresh.VaccineRecords.CountAsync());
    }

    [Fact]
    public async Task Price_keeps_two_decimals()
    {
        var seed = await fixture.ResetAsync();
        await using (var db = fixture.NewContext())
        {
            var service = await db.Services.SingleAsync(s => s.Id == seed.Examination.Id);
            service.Price = 499.99m;
            await db.SaveChangesAsync();
        }

        await using var fresh = fixture.NewContext();
        Assert.Equal(499.99m, (await fresh.Services.SingleAsync(s => s.Id == seed.Examination.Id)).Price);
    }

    [Theory]
    [InlineData("Services", "Price", 10, 2)]
    [InlineData("Appointments", "Price", 10, 2)]
    [InlineData("Pets", "WeightKg", 5, 2)]
    public async Task Decimal_columns_have_expected_precision(string table, string column, int precision, int scale)
    {
        await using var db = fixture.NewContext();

        var actual = await db.Database.SqlQuery<int>($"""
            SELECT CAST(NUMERIC_PRECISION AS int) * 100 + CAST(NUMERIC_SCALE AS int) AS Value
            FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = {table} AND COLUMN_NAME = {column}
            """).SingleAsync();

        Assert.Equal(precision * 100 + scale, actual);
    }

    [Fact]
    public async Task Clinic_cannot_have_same_day_twice()
    {
        await fixture.ResetAsync();
        await using var db = fixture.NewContext();
        var clinic = await db.Clinics.SingleAsync();
        db.Add(new ClinicHours
        {
            ClinicId = clinic.Id,
            Day = DayOfWeek.Monday,
            Opens = new TimeOnly(10, 0),
            Closes = new TimeOnly(11, 0),
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task User_cannot_be_two_veterinarians()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();
        db.Veterinarians.Add(new Veterinarian { UserId = seed.VetA.UserId, Title = "Dr.", IsActive = true });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Token_hash_is_unique()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();
        for (var i = 0; i < 2; i++)
        {
            db.AccessTokens.Add(new AccessToken
            {
                UserId = seed.OwnerA.Id,
                Name = $"token {i}",
                TokenHash = new string('a', 64),
                CreatedAt = Monday9,
                ExpiresAt = Monday9.AddDays(30),
            });
        }

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Concurrent_status_change_is_detected()
    {
        var seed = await fixture.ResetAsync();
        var appointment = seed.NewAppointment(Monday9);
        await using (var db = fixture.NewContext())
        {
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
        }

        await using var first = fixture.NewContext();
        await using var second = fixture.NewContext();
        (await first.Appointments.SingleAsync()).Status = AppointmentStatus.Approved;
        (await second.Appointments.SingleAsync()).Status = AppointmentStatus.Rejected;
        await first.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
}
