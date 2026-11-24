using Microsoft.EntityFrameworkCore;
using VetKlinik.Web.Data;

namespace VetKlinik.Tests.Infrastructure;

/// <summary>Her test sınıfı için gerçek SQL Server üzerinde geçici bir veritabanı açar, sonunda siler.</summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public string ConnectionString { get; } = TestConnection.ForDatabase($"VetKlinikTest_{Guid.NewGuid():N}");

    public AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = NewContext();
        await db.Database.EnsureDeletedAsync();
    }

    /// <summary>Testler birbirini etkilemesin diye tabloları boşaltıp örnek veriyi yeniden yükler.</summary>
    public async Task<Seeded> ResetAsync()
    {
        await ClearAsync();
        await using var db = NewContext();
        return await TestData.SeedAsync(db);
    }

    /// <summary>Tüm tabloları boşaltır, örnek veri yüklemez.</summary>
    public async Task ClearAsync()
    {
        await using var db = NewContext();
        await db.AccessTokens.ExecuteDeleteAsync();
        await db.Appointments.ExecuteDeleteAsync();
        await db.VaccineRecords.ExecuteDeleteAsync();
        await db.Pets.ExecuteDeleteAsync();
        await db.Veterinarians.ExecuteDeleteAsync();
        await db.Services.ExecuteDeleteAsync();
        await db.Specialties.ExecuteDeleteAsync();
        await db.Clinics.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
        await db.Roles.ExecuteDeleteAsync();
    }
}
