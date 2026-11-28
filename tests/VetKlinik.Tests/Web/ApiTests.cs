using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VetKlinik.Tests.Infrastructure;
using VetKlinik.Web.Domain;

namespace VetKlinik.Tests.Web;

/// <summary>REST API: kimlik doğrulama, yetki (404/403/401), DTO biçimi ve randevu oluşturma.</summary>
public class ApiTests(WebFixture web) : IClassFixture<WebFixture>
{
    /// <summary>Bugünden en az 2 gün sonraki ilk hafta içi gün (uygulama gerçek saati kullanır).</summary>
    private static DateOnly NextWeekday()
    {
        var day = DateOnly.FromDateTime(DateTime.Today).AddDays(2);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            day = day.AddDays(1);
        return day;
    }

    private Task<HttpClient> As(string email) => web.LoginClientAsync(email, TestData.Password);

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static object Booking(Seeded seed, DateTime startsAt, int? petId = null) => new
    {
        petId = petId ?? seed.PetA.Id,
        serviceId = seed.Examination.Id,
        veterinarianId = seed.VetA.Id,
        startsAt = startsAt.ToString("yyyy-MM-ddTHH:mm:ss"),
        price = 1, // DTO'da yok, yok sayılmalı
        status = "Completed",
    };

    [Theory]
    [InlineData("/api/veterinarians")]
    [InlineData("/api/reports/services")]
    [InlineData("/api/appointments")]
    public async Task Anonymous_gets_401_not_redirect(string url)
    {
        await web.Database.ResetAsync();

        var response = await web.NewClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Veterinarians_returns_dto_without_personal_data()
    {
        await web.Database.ResetAsync();
        var client = await As("ownera@test.local");

        var response = await client.GetAsync("/api/veterinarians");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("Kısırlaştırma", raw); // Türkçe karakter kaçışsız
        Assert.DoesNotContain("@", raw);
        Assert.DoesNotContain("PasswordHash", raw);
        var vets = JsonDocument.Parse(raw).RootElement;
        Assert.Equal(2, vets.GetArrayLength());
        var a = vets.EnumerateArray().Single(v => v.GetProperty("fullName").GetString() == "Hekim A");
        Assert.Equal("Cerrahi", a.GetProperty("specialties")[0].GetString());
        Assert.Equal(2, a.GetProperty("services").GetArrayLength());
    }

    [Fact]
    public async Task Available_lists_slots_that_fit_the_service_duration()
    {
        var seed = await web.Database.ResetAsync();
        var client = await As("ownera@test.local");
        var day = NextWeekday();

        var response = await client.GetAsync($"/api/veterinarians/available?serviceId={seed.Neutering.Id}&date={day:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal(90, body.GetProperty("durationMinutes").GetInt32());
        var vet = Assert.Single(body.GetProperty("veterinarians").EnumerateArray()); // yalnız Hekim A kısırlaştırma yapar
        Assert.Equal(seed.VetA.Id, vet.GetProperty("veterinarianId").GetInt32());
        var slots = vet.GetProperty("slots").EnumerateArray().Select(s => s.GetDateTime()).ToList();
        Assert.Equal(day.ToDateTime(new TimeOnly(9, 0)), slots[0]);
        Assert.Equal(day.ToDateTime(new TimeOnly(16, 30)), slots[^1]); // 16:30 + 90 dk = 18:00
    }

    [Fact]
    public async Task Available_validates_input()
    {
        var seed = await web.Database.ResetAsync();
        var client = await As("ownera@test.local");

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/veterinarians/available?serviceId=999999&date={NextWeekday():yyyy-MM-dd}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync($"/api/veterinarians/available?serviceId={seed.Examination.Id}")).StatusCode);
    }

    [Fact]
    public async Task Pet_vaccines_visible_to_owner_only()
    {
        var seed = await web.Database.ResetAsync();
        await using (var db = web.Database.NewContext())
        {
            db.VaccineRecords.Add(new VaccineRecord
            {
                PetId = seed.PetA.Id,
                VaccineName = "Karma Aşı",
                AdministeredOn = DateOnly.FromDateTime(DateTime.Today).AddDays(-10),
                NextDueOn = DateOnly.FromDateTime(DateTime.Today).AddDays(20),
            });
            await db.SaveChangesAsync();
        }

        var owner = await (await As("ownera@test.local")).GetAsync($"/api/pets/{seed.PetA.Id}/vaccines");
        var other = await (await As("ownerb@test.local")).GetAsync($"/api/pets/{seed.PetA.Id}/vaccines");

        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);
        var body = await JsonAsync(owner);
        Assert.Equal("Pamuk", body.GetProperty("petName").GetString());
        Assert.Equal("Karma Aşı", body.GetProperty("history")[0].GetProperty("vaccineName").GetString());
        var next = body.GetProperty("upcoming")[0];
        Assert.Equal(20, next.GetProperty("daysLeft").GetInt32());
        Assert.False(next.GetProperty("isOverdue").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }

    [Fact]
    public async Task Report_is_admin_only_and_grouped()
    {
        var seed = await web.Database.ResetAsync();
        await using (var db = web.Database.NewContext())
        {
            var done = seed.NewAppointment(new DateTime(2026, 11, 10, 10, 0, 0));
            done.Status = AppointmentStatus.Completed;
            var cancelled = seed.NewAppointment(new DateTime(2026, 11, 11, 10, 0, 0));
            cancelled.Status = AppointmentStatus.Cancelled;
            db.Appointments.AddRange(done, cancelled);
            await db.SaveChangesAsync();
        }

        var member = await (await As("ownera@test.local")).GetAsync("/api/reports/services?from=2026-11-01&to=2026-11-30");
        var admin = await As("admin@test.local");
        var ok = await admin.GetAsync("/api/reports/services?from=2026-11-01&to=2026-11-30");
        var reversed = await admin.GetAsync("/api/reports/services?from=2026-11-30&to=2026-11-01");

        Assert.Equal(HttpStatusCode.Forbidden, member.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var body = await JsonAsync(ok);
        Assert.Equal(500m, body.GetProperty("totalRevenue").GetDecimal());
        var row = Assert.Single(body.GetProperty("services").EnumerateArray());
        Assert.Equal("Muayene", row.GetProperty("serviceName").GetString());
        Assert.Equal(1, row.GetProperty("appointmentCount").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, reversed.StatusCode);
    }

    [Fact]
    public async Task Member_creates_appointment_and_gets_201()
    {
        var seed = await web.Database.ResetAsync();
        var client = await As("ownera@test.local");
        var startsAt = NextWeekday().ToDateTime(new TimeOnly(10, 0));

        var response = await client.PostAsJsonAsync("/api/appointments", Booking(seed, startsAt));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await JsonAsync(response);
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        Assert.Equal("Onay bekliyor", body.GetProperty("statusName").GetString());
        Assert.Equal(500m, body.GetProperty("price").GetDecimal());
        Assert.Equal(startsAt.AddMinutes(30), body.GetProperty("endsAt").GetDateTime());

        var location = response.Headers.Location!;
        var fetched = await client.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(body.GetProperty("id").GetInt32(), (await JsonAsync(fetched)).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Conflicting_appointment_gets_400_with_reason()
    {
        var seed = await web.Database.ResetAsync();
        var client = await As("ownera@test.local");
        var startsAt = NextWeekday().ToDateTime(new TimeOnly(10, 0));
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/appointments", Booking(seed, startsAt))).StatusCode);

        var response = await client.PostAsJsonAsync("/api/appointments", Booking(seed, startsAt.AddMinutes(15)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await JsonAsync(response)).GetProperty("errors");
        Assert.Contains("Hekimin bu saati dolu. Lütfen başka bir saat seçin.", errors.EnumerateArray().Select(e => e.GetString()));
        await using var db = web.Database.NewContext();
        Assert.Equal(1, await db.Appointments.CountAsync());
    }

    [Fact]
    public async Task Invalid_body_gets_400()
    {
        var seed = await web.Database.ResetAsync();
        var client = await As("ownera@test.local");

        var response = await client.PostAsJsonAsync("/api/appointments", new { petId = seed.PetA.Id, serviceId = seed.Examination.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Saat seçiniz.", await response.Content.ReadAsStringAsync());

        var empty = await client.PostAsync("/api/appointments", new StringContent("", System.Text.Encoding.UTF8, "application/json"));
        var broken = await client.PostAsync("/api/appointments", new StringContent("{\"petId\":\"x\"}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Contains("İstek gövdesi boş", await empty.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, broken.StatusCode);
        var brokenBody = await broken.Content.ReadAsStringAsync();
        Assert.Contains("Geçersiz değer: petId", brokenBody);
        Assert.DoesNotContain("field is required", brokenBody);
    }

    [Fact]
    public async Task Other_members_pet_and_appointment_are_404()
    {
        var seed = await web.Database.ResetAsync();
        var owner = await As("ownera@test.local");
        var other = await As("ownerb@test.local");
        var startsAt = NextWeekday().ToDateTime(new TimeOnly(11, 0));
        var created = await owner.PostAsJsonAsync("/api/appointments", Booking(seed, startsAt));
        var id = (await JsonAsync(created)).GetProperty("id").GetInt32();

        var book = await other.PostAsJsonAsync("/api/appointments", Booking(seed, startsAt.AddHours(2)));
        var read = await other.GetAsync($"/api/appointments/{id}");
        var list = await JsonAsync(await other.GetAsync("/api/appointments"));

        Assert.Equal(HttpStatusCode.NotFound, book.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(0, list.GetArrayLength());
    }

    [Fact]
    public async Task Vet_sees_own_appointments_in_api()
    {
        var seed = await web.Database.ResetAsync();
        var startsAt = NextWeekday().ToDateTime(new TimeOnly(12, 0));
        await (await As("ownera@test.local")).PostAsJsonAsync("/api/appointments", Booking(seed, startsAt));

        var vetA = await JsonAsync(await (await As("veta@test.local")).GetAsync("/api/appointments"));
        var vetB = await JsonAsync(await (await As("vetb@test.local")).GetAsync("/api/appointments"));

        Assert.Equal("Pamuk", vetA[0].GetProperty("petName").GetString());
        Assert.Equal(0, vetB.GetArrayLength());
    }

    [Fact]
    public async Task Errors_are_turkish_problem_json()
    {
        await web.Database.ResetAsync();
        var anonymous = await web.NewClient().GetAsync("/api/veterinarians");
        var vet = await As("veta@test.local");
        var forbidden = await vet.GetAsync("/api/reports/services");
        var unknown = await vet.GetAsync("/api/olmayan");
        var badId = await vet.GetAsync("/api/appointments/abc");
        var anonymousPost = await web.NewClient().PostAsJsonAsync("/api/appointments", new { petId = 1 });

        await AssertProblemAsync(anonymous, HttpStatusCode.Unauthorized, "Giriş yapmanız gerekiyor.");
        await AssertProblemAsync(forbidden, HttpStatusCode.Forbidden, "Bu işlem için yetkiniz yok.");
        await AssertProblemAsync(unknown, HttpStatusCode.NotFound, "bulunamadı");
        await AssertProblemAsync(badId, HttpStatusCode.NotFound, "bulunamadı");
        await AssertProblemAsync(anonymousPost, HttpStatusCode.Unauthorized, "Giriş yapmanız gerekiyor.");
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string text)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await JsonAsync(response);
        Assert.Contains(text, body.GetProperty("title").GetString());
        Assert.NotEmpty(body.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public async Task Rule_errors_are_problem_json()
    {
        var seed = await web.Database.ResetAsync();
        var client = await As("ownera@test.local");
        var startsAt = NextWeekday().ToDateTime(new TimeOnly(10, 0));
        await client.PostAsJsonAsync("/api/appointments", Booking(seed, startsAt));

        await AssertProblemAsync(await client.PostAsJsonAsync("/api/appointments", Booking(seed, startsAt)),
            HttpStatusCode.BadRequest, "Randevu oluşturulamadı.");
        await AssertProblemAsync(await client.GetAsync($"/api/pets/{seed.PetB.Id}/vaccines"),
            HttpStatusCode.NotFound, "Hayvan bulunamadı.");
    }

    // Çerezle kimlik doğrulanan POST'a karşı CSRF koruması: yalnız JSON kabul edilir (form/text başka siteden
    // ön kontrolsüz gönderilebilir, JSON gönderilemez) ve çerez SameSite=Lax'tır.
    [Theory]
    [InlineData("application/x-www-form-urlencoded", "petId=1")]
    [InlineData("text/plain", "{\"petId\":1}")]
    public async Task Non_json_post_is_415(string contentType, string body)
    {
        await web.Database.ResetAsync();
        var client = await As("ownera@test.local");

        var response = await client.PostAsync("/api/appointments", new StringContent(body, System.Text.Encoding.UTF8, contentType));

        await AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "JSON");
    }

    [Theory]
    [InlineData("from=01.11.2026&to=30.11.2026", "yyyy-MM-dd")]       // Türkçe yazım sessizce yanlış okunmaz
    [InlineData("from=2025-01-01&to=2026-11-30", "en fazla bir yıl")]
    [InlineData("from=2026-01-01&to=9999-12-31", "Tarih geçersiz")]
    [InlineData("from=2026-11-30&to=2026-11-01", "Başlangıç tarihi")]
    public async Task Report_rejects_bad_ranges(string query, string message)
    {
        await web.Database.ResetAsync();
        var admin = await As("admin@test.local");

        var response = await admin.GetAsync($"/api/reports/services?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(message, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("to=2025-02-10", "2025-02-01", "2025-02-10")]
    [InlineData("from=2025-02-10", "2025-02-10", "2025-02-28")]
    public async Task Report_fills_missing_bound_from_the_given_month(string query, string from, string to)
    {
        await web.Database.ResetAsync();
        var admin = await As("admin@test.local");

        var body = await JsonAsync(await admin.GetAsync($"/api/reports/services?{query}"));

        Assert.Equal(from, body.GetProperty("from").GetString());
        Assert.Equal(to, body.GetProperty("to").GetString());
    }

    [Fact]
    public async Task Available_explains_unusable_dates_and_vets()
    {
        var seed = await web.Database.ResetAsync();
        var client = await As("ownera@test.local");
        var url = $"/api/veterinarians/available?serviceId={seed.Examination.Id}&date=";
        var today = DateOnly.FromDateTime(DateTime.Today);

        var past = await client.GetAsync(url + today.AddDays(-1).ToString("yyyy-MM-dd"));
        var far = await client.GetAsync(url + today.AddDays(61).ToString("yyyy-MM-dd"));
        var turkish = await client.GetAsync(url + NextWeekday().ToString("dd.MM.yyyy"));
        var noVet = await client.GetAsync(url + NextWeekday().ToString("yyyy-MM-dd") + "&veterinarianId=999999");
        var badService = await client.GetAsync($"/api/veterinarians/available?serviceId=abc&date={NextWeekday():yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        Assert.Contains("geçmiş", await past.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, far.StatusCode);
        Assert.Contains("60 gün", await far.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, turkish.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, noVet.StatusCode);
        Assert.Contains("serviceId", await badService.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Other_vet_cannot_read_appointment_but_own_vet_sees_vaccines()
    {
        var seed = await web.Database.ResetAsync();
        var created = await (await As("ownera@test.local")).PostAsJsonAsync("/api/appointments",
            Booking(seed, NextWeekday().ToDateTime(new TimeOnly(13, 0))));
        var id = (await JsonAsync(created)).GetProperty("id").GetInt32();
        var vetA = await As("veta@test.local");
        var vetB = await As("vetb@test.local");

        Assert.Equal(HttpStatusCode.OK, (await vetA.GetAsync($"/api/appointments/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await vetB.GetAsync($"/api/appointments/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await vetA.GetAsync($"/api/pets/{seed.PetA.Id}/vaccines")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await vetB.GetAsync($"/api/pets/{seed.PetA.Id}/vaccines")).StatusCode);
    }

    [Fact]
    public async Task Utc_start_time_is_converted_to_clinic_time()
    {
        var seed = await web.Database.ResetAsync();
        var client = await As("ownera@test.local");
        var local = NextWeekday().ToDateTime(new TimeOnly(14, 0));

        var response = await client.PostAsJsonAsync("/api/appointments", new
        {
            petId = seed.PetA.Id, serviceId = seed.Examination.Id, veterinarianId = seed.VetA.Id,
            startsAt = local.AddHours(-3).ToString("yyyy-MM-ddTHH:mm:ss") + "Z", // İstanbul UTC+3
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(local, (await JsonAsync(response)).GetProperty("startsAt").GetDateTime());
    }
}
