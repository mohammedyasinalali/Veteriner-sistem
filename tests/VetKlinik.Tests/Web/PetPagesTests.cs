using System.Net;
using Microsoft.EntityFrameworkCore;
using VetKlinik.Tests.Infrastructure;

namespace VetKlinik.Tests.Web;

public class PetPagesTests(WebFixture web) : IClassFixture<WebFixture>
{
    private static Dictionary<string, string> PetFields(string name) => new()
    {
        ["Name"] = name,
        ["Species"] = "Cat",
        ["Breed"] = "Van",
        ["BirthDate"] = "2024-05-01",
        ["WeightKg"] = "3,75",
    };

    private static async Task<string> CreatePetAsync(HttpClient client, string name)
    {
        var response = await WebFixture.PostFormAsync(client, "/Pets/Create", PetFields(name));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return response.Headers.Location!.ToString(); // /Pets/Details/{id}
    }

    [Fact]
    public async Task Member_creates_pet_and_sees_it()
    {
        var member = await web.MemberClientAsync("hayvansever@test.local");

        var details = await CreatePetAsync(member, "Minnoş");

        Assert.Contains("/Pets/Details/", details);
        var html = await member.GetStringAsync(details);
        Assert.Contains("Minnoş", html);
        Assert.Contains("3,75", html);
        Assert.Contains("Minnoş", await member.GetStringAsync("/Pets"));
    }

    [Fact]
    public async Task Other_member_gets_404_for_details_edit_and_delete()
    {
        var owner = await web.MemberClientAsync("sahip@test.local");
        var details = await CreatePetAsync(owner, "Duman");
        var id = details.Split('/').Last();
        var intruder = await web.MemberClientAsync("merakli@test.local");

        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/Pets/Details/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/Pets/Edit/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/Pets/Delete/{id}")).StatusCode);

        // POST'lar: antiforgery token'ı saldırganın kendi formundan alınır.
        var edit = await WebFixture.PostFormAsync(intruder, $"/Pets/Edit/{id}", PetFields("Çalındı"), formPageUrl: "/Pets/Create");
        var delete = await WebFixture.PostFormAsync(intruder, $"/Pets/Delete/{id}", [], formPageUrl: "/Pets/Create");
        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

        await using var db = web.Database.NewContext();
        var pet = await db.Pets.SingleAsync(p => p.Id == int.Parse(id));
        Assert.Equal("Duman", pet.Name);
        Assert.DoesNotContain("Duman", await intruder.GetStringAsync("/Pets"));
    }

    [Fact]
    public async Task Hidden_owner_field_is_ignored()
    {
        var member = await web.MemberClientAsync("gizli@test.local");
        var fields = PetFields("Tarçın");
        fields["OwnerId"] = "baskasinin-id";

        var response = await WebFixture.PostFormAsync(member, "/Pets/Create", fields);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var details = response.Headers.Location!.ToString();

        await using var db = web.Database.NewContext();
        var pet = await db.Pets.Include(p => p.Owner).SingleAsync(p => p.Id == int.Parse(details.Split('/').Last()));
        Assert.Equal("gizli@test.local", pet.Owner.Email);
    }

    [Fact]
    public async Task Owner_deletes_pet()
    {
        var member = await web.MemberClientAsync("silen@test.local");
        var id = (await CreatePetAsync(member, "Fıstık")).Split('/').Last();

        var response = await WebFixture.PostFormAsync(member, $"/Pets/Delete/{id}", []);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/Pets/Details/{id}")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_redirected_to_login()
    {
        var response = await web.NewClient().GetAsync("/Pets");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }
}
