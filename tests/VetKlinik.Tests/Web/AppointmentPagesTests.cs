using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using VetKlinik.Tests.Infrastructure;

namespace VetKlinik.Tests.Web;

public partial class AppointmentPagesTests(WebFixture web) : IClassFixture<WebFixture>
{
    /// <summary>Bugünden en az 2 gün sonraki ilk hafta içi gün (uygulama gerçek saati kullanır).</summary>
    private static DateOnly NextWeekday()
    {
        var day = DateOnly.FromDateTime(DateTime.Today).AddDays(2);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            day = day.AddDays(1);
        return day;
    }

    private async Task<(HttpClient Member, int PetId, int ServiceId)> SetupAsync(string email, string vetEmail)
    {
        var admin = await web.AdminClientAsync();
        int serviceId;
        await using (var db = web.Database.NewContext())
            serviceId = (await db.Services.SingleAsync(s => s.Name == "Muayene")).Id;
        var vet = await WebFixture.PostFormAsync(admin, "/Admin/Veterinarians/Create", new()
        {
            ["FullName"] = "Dr. Randevu " + vetEmail[..3],
            ["Email"] = vetEmail,
            ["Password"] = "hekim123",
            ["Title"] = "Vet. Hek.",
            ["IsActive"] = "true",
            ["ServiceIds"] = serviceId.ToString(),
            ["Days[0].Day"] = "Monday", ["Days[0].Works"] = "true", ["Days[0].Starts"] = "09:00", ["Days[0].Ends"] = "18:00",
            ["Days[1].Day"] = "Tuesday", ["Days[1].Works"] = "true", ["Days[1].Starts"] = "09:00", ["Days[1].Ends"] = "18:00",
            ["Days[2].Day"] = "Wednesday", ["Days[2].Works"] = "true", ["Days[2].Starts"] = "09:00", ["Days[2].Ends"] = "18:00",
            ["Days[3].Day"] = "Thursday", ["Days[3].Works"] = "true", ["Days[3].Starts"] = "09:00", ["Days[3].Ends"] = "18:00",
            ["Days[4].Day"] = "Friday", ["Days[4].Works"] = "true", ["Days[4].Starts"] = "09:00", ["Days[4].Ends"] = "18:00",
        });
        Assert.Equal(HttpStatusCode.Redirect, vet.StatusCode);

        var member = await web.MemberClientAsync(email);
        var pet = await WebFixture.PostFormAsync(member, "/Pets/Create", new()
        {
            ["Name"] = "Limon", ["Species"] = "Dog", ["Breed"] = "Terrier", ["BirthDate"] = "2022-02-02", ["WeightKg"] = "8",
        });
        return (member, int.Parse(pet.Headers.Location!.ToString().Split('/').Last()), serviceId);
    }

    [GeneratedRegex("name=\"VeterinarianId\" value=\"(\\d+)\"[^>]*>\\s*<input[^>]*name=\"StartsAt\" value=\"([^\"]+)\"")]
    private static partial Regex SlotRegex();

    [Fact]
    public async Task Member_books_a_listed_slot_and_sees_it_pending()
    {
        var (member, petId, serviceId) = await SetupAsync("randevucu@test.local", "rnd1@test.local");
        var day = NextWeekday();
        var bookUrl = $"/Appointments/Book?petId={petId}&serviceId={serviceId}&date={day:yyyy-MM-dd}";

        var page = await member.GetStringAsync(bookUrl);
        var slot = SlotRegex().Match(page);
        Assert.True(slot.Success, "Sayfada uygun saat bulunamadı.");

        var response = await WebFixture.PostFormAsync(member, "/Appointments/Book", new()
        {
            ["PetId"] = petId.ToString(),
            ["ServiceId"] = serviceId.ToString(),
            ["VeterinarianId"] = slot.Groups[1].Value,
            ["StartsAt"] = slot.Groups[2].Value,
            ["Price"] = "1", // yok sayılmalı
        }, formPageUrl: bookUrl);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var list = await member.GetStringAsync("/Appointments");
        Assert.Contains("Limon", list);
        Assert.Contains("Onay bekliyor", list);
        await using var db = web.Database.NewContext();
        var saved = await db.Appointments.SingleAsync(a => a.PetId == petId);
        Assert.Equal(500m, saved.Price);
        Assert.Equal(day.ToDateTime(new TimeOnly(9, 0)), saved.StartsAt);
    }

    [Fact]
    public async Task Booking_for_another_members_pet_returns_404()
    {
        var (_, petId, serviceId) = await SetupAsync("kurban@test.local", "rnd2@test.local");
        var intruder = await web.MemberClientAsync("davetsiz@test.local");
        var day = NextWeekday();

        var get = await intruder.GetAsync($"/Appointments/Book?petId={petId}&serviceId={serviceId}&date={day:yyyy-MM-dd}");
        var post = await WebFixture.PostFormAsync(intruder, "/Appointments/Book", new()
        {
            ["PetId"] = petId.ToString(), ["ServiceId"] = serviceId.ToString(), ["VeterinarianId"] = "1",
            ["StartsAt"] = day.ToDateTime(new TimeOnly(10, 0)).ToString("yyyy-MM-ddTHH:mm"),
        }, formPageUrl: "/Appointments/Book");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        await using var db = web.Database.NewContext();
        Assert.False(await db.Appointments.AnyAsync(a => a.PetId == petId));
    }

    [Fact]
    public async Task Taken_slot_shows_turkish_error()
    {
        var (member, petId, serviceId) = await SetupAsync("tekrar@test.local", "rnd3@test.local");
        var day = NextWeekday();
        int vetId;
        await using (var db = web.Database.NewContext())
            vetId = (await db.Veterinarians.Include(v => v.User).SingleAsync(v => v.User.Email == "rnd3@test.local")).Id;
        var fields = new Dictionary<string, string>
        {
            ["PetId"] = petId.ToString(), ["ServiceId"] = serviceId.ToString(), ["VeterinarianId"] = vetId.ToString(),
            ["StartsAt"] = day.ToDateTime(new TimeOnly(11, 0)).ToString("yyyy-MM-ddTHH:mm"),
        };
        Assert.Equal(HttpStatusCode.Redirect, (await WebFixture.PostFormAsync(member, "/Appointments/Book", new(fields), formPageUrl: "/Appointments/Book")).StatusCode);

        var again = await WebFixture.PostFormAsync(member, "/Appointments/Book", new(fields), formPageUrl: "/Appointments/Book");

        Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
        Assert.Contains("/Appointments/Book", again.Headers.Location!.ToString());
        var page = await member.GetStringAsync(again.Headers.Location!.ToString());
        Assert.Contains("dolu", page);
    }

    [Fact]
    public async Task Anonymous_is_redirected_to_login()
    {
        var response = await web.NewClient().GetAsync("/Appointments/Book");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }
}
