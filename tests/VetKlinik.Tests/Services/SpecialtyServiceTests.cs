using Microsoft.EntityFrameworkCore;
using VetKlinik.Tests.Infrastructure;
using VetKlinik.Web.Domain;
using VetKlinik.Web.Services;

namespace VetKlinik.Tests.Services;

public class SpecialtyServiceTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task Create_rejects_duplicate_turkish_name()
    {
        await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var result = await new SpecialtyService(db).CreateAsync("CERRAHİ");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task List_counts_veterinarians()
    {
        await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var rows = await new SpecialtyService(db).ListAsync();

        Assert.Equal(1, Assert.Single(rows, r => r.Name == "Cerrahi").VeterinarianCount);
    }

    [Fact]
    public async Task Delete_in_use_is_refused()
    {
        await fixture.ResetAsync();
        await using var db = fixture.NewContext();
        var surgery = await db.Specialties.SingleAsync(s => s.Name == "Cerrahi");

        var result = await new SpecialtyService(db).DeleteAsync(surgery.Id);

        Assert.False(result.Succeeded);
        Assert.True(await db.Specialties.AnyAsync(s => s.Id == surgery.Id));
    }

    [Fact]
    public async Task Delete_unused_removes()
    {
        await fixture.ResetAsync();
        await using (var setup = fixture.NewContext())
        {
            setup.Specialties.Add(new Specialty { Name = "Dermatoloji" });
            await setup.SaveChangesAsync();
        }
        await using var db = fixture.NewContext();
        var id = (await db.Specialties.SingleAsync(s => s.Name == "Dermatoloji")).Id;

        var result = await new SpecialtyService(db).DeleteAsync(id);

        Assert.True(result.Succeeded);
        Assert.False(await db.Specialties.AnyAsync(s => s.Id == id));
    }
}
