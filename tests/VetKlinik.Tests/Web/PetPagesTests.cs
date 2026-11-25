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

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    /// <summary>Fotoğraflı formu multipart olarak gönderir (antiforgery token'ı form sayfasından okunur).</summary>
    private static async Task<HttpResponseMessage> PostMultipartAsync(HttpClient client, string url, Dictionary<string, string> fields, byte[]? photo)
    {
        var page = await client.GetStringAsync(url);
        var token = System.Text.RegularExpressions.Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var content = new MultipartFormDataContent { { new StringContent(token), "__RequestVerificationToken" } };
        foreach (var (key, value) in fields)
            content.Add(new StringContent(value), key);
        if (photo is not null)
            content.Add(new ByteArrayContent(photo), "Photo", "foto.png");
        return await client.PostAsync(url, content);
    }

    [Fact]
    public async Task Photo_is_served_only_to_users_who_can_see_the_pet_and_removed_on_request()
    {
        var owner = await web.MemberClientAsync("fotograf@test.local");
        var create = await PostMultipartAsync(owner, "/Pets/Create", PetFields("Bulut"), Png);
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        var id = create.Headers.Location!.ToString().Split('/').Last();

        var photo = await owner.GetAsync($"/Pets/Photo/{id}");
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/png", photo.Content.Headers.ContentType!.MediaType);
        Assert.Single(Directory.GetFiles(web.UploadsPath));

        var intruder = await web.MemberClientAsync("fotografci@test.local");
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/Pets/Photo/{id}")).StatusCode);

        var fields = PetFields("Bulut");
        fields["RemovePhoto"] = "true";
        var edit = await PostMultipartAsync(owner, $"/Pets/Edit/{id}", fields, photo: null);
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/Pets/Photo/{id}")).StatusCode);
        Assert.Empty(Directory.GetFiles(web.UploadsPath));
    }

    [Fact]
    public async Task Vet_can_view_patient_but_not_edit_or_delete()
    {
        var owner = await web.MemberClientAsync("hasta@test.local");
        var id = int.Parse((await CreatePetAsync(owner, "Zeytin")).Split('/').Last());

        var admin = await web.AdminClientAsync();
        var vetCreate = await WebFixture.PostFormAsync(admin, "/Admin/Veterinarians/Create", new()
        {
            ["FullName"] = "Dr. Can Aydın",
            ["Email"] = "can@test.local",
            ["Password"] = "hekim123",
            ["Title"] = "Vet. Hek.",
            ["IsActive"] = "true",
        });
        Assert.Equal(HttpStatusCode.Redirect, vetCreate.StatusCode);
        await using (var db = web.Database.NewContext())
        {
            var vet = await db.Veterinarians.Include(v => v.User).SingleAsync(v => v.User.Email == "can@test.local");
            var service = await db.Services.FirstAsync();
            var start = new DateTime(2026, 12, 7, 10, 0, 0);
            db.Appointments.Add(new VetKlinik.Web.Domain.Appointment
            {
                PetId = id, VeterinarianId = vet.Id, ServiceId = service.Id, StartsAt = start,
                EndsAt = start.AddMinutes(service.DurationMinutes), DurationMinutes = service.DurationMinutes,
                Price = service.Price, CreatedAt = start.AddDays(-1),
            });
            await db.SaveChangesAsync();
        }
        var vetClient = web.NewClient();
        await WebFixture.PostFormAsync(vetClient, "/Account/Login", new() { ["Email"] = "can@test.local", ["Password"] = "hekim123" });

        Assert.Contains("Zeytin", await vetClient.GetStringAsync("/Pets"));
        Assert.Equal(HttpStatusCode.OK, (await vetClient.GetAsync($"/Pets/Details/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await vetClient.GetAsync($"/Pets/Edit/{id}")).StatusCode);
        var delete = await WebFixture.PostFormAsync(vetClient, $"/Pets/Delete/{id}", [], formPageUrl: $"/Pets/Details/{id}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_redirected_to_login()
    {
        var response = await web.NewClient().GetAsync("/Pets");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }
}
