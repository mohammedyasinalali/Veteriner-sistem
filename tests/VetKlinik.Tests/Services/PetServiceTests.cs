using Microsoft.EntityFrameworkCore;
using VetKlinik.Tests.Infrastructure;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Data;
using VetKlinik.Web.Domain;
using VetKlinik.Web.Models.Pets;
using VetKlinik.Web.Services;

namespace VetKlinik.Tests.Services;

public class PetServiceTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>, IDisposable
{
    private readonly string photoFolder = Path.Combine(Path.GetTempPath(), $"vetklinik-photos-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(photoFolder))
            Directory.Delete(photoFolder, recursive: true);
    }

    private PetService Service(AppDbContext db) => new(db, new PhotoStorage(photoFolder), TimeProvider.System);

    private static CurrentUser Member(ApplicationUser u) => new(u.Id, IsAdmin: false, IsVeterinarian: false);
    private static CurrentUser Admin(ApplicationUser u) => new(u.Id, IsAdmin: true, IsVeterinarian: false);
    private static CurrentUser Vet(Veterinarian v) => new(v.UserId, IsAdmin: false, IsVeterinarian: true);

    private static PetFormModel Form(string name = "Boncuk") => new()
    {
        Name = name,
        Species = Species.Dog,
        Breed = "Golden",
        BirthDate = new DateOnly(2023, 3, 10),
        WeightKg = 12.5m,
    };

    [Fact]
    public async Task Member_lists_only_own_pets()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var pets = await Service(db).ListAsync(Member(seed.OwnerA));

        Assert.Equal(["Pamuk"], pets.Select(p => p.Name));
    }

    [Fact]
    public async Task Admin_lists_all_pets()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var pets = await Service(db).ListAsync(Admin(seed.Admin));

        Assert.Equal(2, pets.Count);
    }

    [Fact]
    public async Task Vet_sees_only_pets_with_appointment()
    {
        var seed = await fixture.ResetAsync();
        await using (var setup = fixture.NewContext())
        {
            setup.Appointments.Add(seed.NewAppointment(new DateTime(2026, 12, 7, 10, 0, 0))); // PetA ↔ VetA
            await setup.SaveChangesAsync();
        }
        await using var db = fixture.NewContext();
        var service = Service(db);

        Assert.Equal(["Pamuk"], (await service.ListAsync(Vet(seed.VetA))).Select(p => p.Name));
        Assert.Empty(await service.ListAsync(Vet(seed.VetB)));
        Assert.Null(await service.FindAsync(Vet(seed.VetA), seed.PetB.Id));
        Assert.Null(await service.FindEditableAsync(Vet(seed.VetA), seed.PetA.Id));
    }

    [Fact]
    public async Task Member_cannot_find_other_members_pet()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();
        var service = Service(db);

        Assert.Null(await service.FindAsync(Member(seed.OwnerB), seed.PetA.Id));
        Assert.Null(await service.FindEditableAsync(Member(seed.OwnerB), seed.PetA.Id));
        Assert.NotNull(await service.FindAsync(Member(seed.OwnerA), seed.PetA.Id));
    }

    [Fact]
    public async Task Member_cannot_update_or_delete_other_members_pet()
    {
        var seed = await fixture.ResetAsync();
        await using (var db = fixture.NewContext())
        {
            var service = Service(db);
            Assert.False((await service.UpdateAsync(Member(seed.OwnerB), seed.PetA.Id, Form("Çalındı"))).Succeeded);
            Assert.False((await service.DeleteAsync(Member(seed.OwnerB), seed.PetA.Id)).Succeeded);
        }

        await using var fresh = fixture.NewContext();
        var pet = await fresh.Pets.SingleAsync(p => p.Id == seed.PetA.Id);
        Assert.Equal("Pamuk", pet.Name);
    }

    [Fact]
    public async Task Create_sets_owner_to_current_user()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var result = await Service(db).CreateAsync(Member(seed.OwnerB), Form());

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        await using var fresh = fixture.NewContext();
        var pet = await fresh.Pets.SingleAsync(p => p.Name == "Boncuk");
        Assert.Equal(seed.OwnerB.Id, pet.OwnerId);
        Assert.Equal(12.5m, pet.WeightKg);
    }

    [Fact]
    public async Task Create_rejects_duplicate_name_for_same_owner()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();
        var service = Service(db);

        var duplicate = await service.CreateAsync(Member(seed.OwnerA), Form("PAMUK"));
        var otherOwner = await service.CreateAsync(Member(seed.OwnerB), Form("Pamuk"));

        Assert.False(duplicate.Succeeded);
        Assert.Contains("Pamuk", string.Join(" ", duplicate.Errors), StringComparison.OrdinalIgnoreCase);
        Assert.True(otherOwner.Succeeded, string.Join(" ", otherOwner.Errors));
    }

    [Fact]
    public async Task Update_keeps_own_name_and_changes_fields()
    {
        var seed = await fixture.ResetAsync();
        await using (var db = fixture.NewContext())
        {
            var form = Form("Pamuk");
            form.WeightKg = 5.25m;
            var result = await Service(db).UpdateAsync(Member(seed.OwnerA), seed.PetA.Id, form);
            Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        }

        await using var fresh = fixture.NewContext();
        var pet = await fresh.Pets.SingleAsync(p => p.Id == seed.PetA.Id);
        Assert.Equal(5.25m, pet.WeightKg);
        Assert.Equal(Species.Dog, pet.Species);
        Assert.Equal(seed.OwnerA.Id, pet.OwnerId);
    }

    [Fact]
    public async Task Create_rejects_future_birth_date()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();
        var form = Form();
        form.BirthDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        var result = await Service(db).CreateAsync(Member(seed.OwnerA), form);

        Assert.False(result.Succeeded);
        Assert.Contains("Doğum tarihi", string.Join(" ", result.Errors));
    }

    [Fact]
    public async Task Delete_with_appointment_is_refused()
    {
        var seed = await fixture.ResetAsync();
        await using (var setup = fixture.NewContext())
        {
            setup.Appointments.Add(seed.NewAppointment(new DateTime(2026, 12, 7, 10, 0, 0)));
            await setup.SaveChangesAsync();
        }
        await using var db = fixture.NewContext();

        var result = await Service(db).DeleteAsync(Member(seed.OwnerA), seed.PetA.Id);

        Assert.False(result.Succeeded);
        Assert.Contains("randevu", string.Join(" ", result.Errors));
        await using var fresh = fixture.NewContext();
        Assert.True(await fresh.Pets.AnyAsync(p => p.Id == seed.PetA.Id));
    }

    [Fact]
    public async Task Delete_removes_pet_with_vaccines_and_photo()
    {
        var seed = await fixture.ResetAsync();
        Directory.CreateDirectory(photoFolder);
        var photoPath = Path.Combine(photoFolder, "eski.png");
        await File.WriteAllTextAsync(photoPath, "x");
        await using (var setup = fixture.NewContext())
        {
            var pet = await setup.Pets.SingleAsync(p => p.Id == seed.PetA.Id);
            pet.PhotoFileName = "eski.png";
            setup.VaccineRecords.Add(new VaccineRecord { PetId = pet.Id, VaccineName = "Kuduz", AdministeredOn = new DateOnly(2025, 1, 1) });
            await setup.SaveChangesAsync();
        }
        await using var db = fixture.NewContext();

        var result = await Service(db).DeleteAsync(Member(seed.OwnerA), seed.PetA.Id);

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        await using var fresh = fixture.NewContext();
        Assert.False(await fresh.Pets.AnyAsync(p => p.Id == seed.PetA.Id));
        Assert.False(await fresh.VaccineRecords.AnyAsync());
        Assert.False(File.Exists(photoPath));
    }

    [Fact]
    public async Task Admin_can_update_any_pet()
    {
        var seed = await fixture.ResetAsync();
        await using var db = fixture.NewContext();

        var result = await Service(db).UpdateAsync(Admin(seed.Admin), seed.PetB.Id, Form("Karabaş"));

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
    }
}
