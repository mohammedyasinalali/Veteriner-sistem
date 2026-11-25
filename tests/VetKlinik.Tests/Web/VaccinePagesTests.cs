using System.Net;
using Microsoft.EntityFrameworkCore;
using VetKlinik.Tests.Infrastructure;

namespace VetKlinik.Tests.Web;

public class VaccinePagesTests(WebFixture web) : IClassFixture<WebFixture>
{
    private static async Task<int> CreatePetAsync(HttpClient client, string name)
    {
        var response = await WebFixture.PostFormAsync(client, "/Pets/Create", new()
        {
            ["Name"] = name, ["Species"] = "Dog", ["Breed"] = "Kangal", ["BirthDate"] = "2023-01-01", ["WeightKg"] = "30",
        });
        return int.Parse(response.Headers.Location!.ToString().Split('/').Last());
    }

    [Fact]
    public async Task Owner_adds_vaccine_and_sees_it_on_pet_page()
    {
        var owner = await web.MemberClientAsync("asili@test.local");
        var petId = await CreatePetAsync(owner, "Karabaş");

        var response = await WebFixture.PostFormAsync(owner, $"/Vaccines/Create?petId={petId}", new()
        {
            ["VaccineName"] = "Kuduz",
            ["AdministeredOn"] = "2026-11-01",
            ["NextDueOn"] = "2027-11-01",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var html = await owner.GetStringAsync($"/Pets/Details/{petId}");
        Assert.Contains("Kuduz", html);
        Assert.Contains("01.11.2027", html);
    }

    [Fact]
    public async Task Other_member_gets_404_and_cannot_add_or_delete()
    {
        var owner = await web.MemberClientAsync("asisahibi@test.local");
        var petId = await CreatePetAsync(owner, "Paşa");
        await WebFixture.PostFormAsync(owner, $"/Vaccines/Create?petId={petId}", new()
        {
            ["VaccineName"] = "Karma", ["AdministeredOn"] = "2026-11-01",
        });
        int recordId;
        await using (var db = web.Database.NewContext())
            recordId = (await db.VaccineRecords.SingleAsync(v => v.PetId == petId)).Id;

        var intruder = await web.MemberClientAsync("asimeraklisi@test.local");
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/Vaccines/Create?petId={petId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/Vaccines/Edit/{recordId}")).StatusCode);
        var add = await WebFixture.PostFormAsync(intruder, $"/Vaccines/Create?petId={petId}",
            new() { ["VaccineName"] = "Sahte", ["AdministeredOn"] = "2026-11-01" }, formPageUrl: "/Pets/Create");
        var delete = await WebFixture.PostFormAsync(intruder, $"/Vaccines/Delete/{recordId}", [], formPageUrl: "/Pets/Create");
        Assert.Equal(HttpStatusCode.NotFound, add.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

        await using var fresh = web.Database.NewContext();
        Assert.Equal(["Karma"], await fresh.VaccineRecords.Where(v => v.PetId == petId).Select(v => v.VaccineName).ToListAsync());
    }
}
