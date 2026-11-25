using System.Net;
using Microsoft.EntityFrameworkCore;
using VetKlinik.Tests.Infrastructure;
using VetKlinik.Web.Domain;

namespace VetKlinik.Tests.Web;

public class AppointmentWorkflowPagesTests(WebFixture web) : IClassFixture<WebFixture>
{
    private sealed record Setup(HttpClient Owner, HttpClient Vet, int AppointmentId);

    /// <summary>Yeni hekim + yeni üye + hayvan + 3 gün sonrasına Pending randevu (doğrudan veritabanına).</summary>
    private async Task<Setup> SetupAsync(string tag)
    {
        var admin = await web.AdminClientAsync();
        var vetEmail = $"hekim{tag}@test.local";
        await WebFixture.PostFormAsync(admin, "/Admin/Veterinarians/Create", new()
        {
            ["FullName"] = $"Dr. {tag}", ["Email"] = vetEmail, ["Password"] = "hekim123", ["Title"] = "Vet. Hek.", ["IsActive"] = "true",
        });
        var vet = web.NewClient();
        await WebFixture.PostFormAsync(vet, "/Account/Login", new() { ["Email"] = vetEmail, ["Password"] = "hekim123" });

        var owner = await web.MemberClientAsync($"sahip{tag}@test.local");
        var pet = await WebFixture.PostFormAsync(owner, "/Pets/Create", new()
        {
            ["Name"] = $"Hayvan{tag}", ["Species"] = "Cat", ["Breed"] = "Tekir", ["BirthDate"] = "2022-02-02", ["WeightKg"] = "4",
        });
        var petId = int.Parse(pet.Headers.Location!.ToString().Split('/').Last());

        await using var db = web.Database.NewContext();
        var vetId = (await db.Veterinarians.Include(v => v.User).SingleAsync(v => v.User.Email == vetEmail)).Id;
        var service = await db.Services.FirstAsync();
        var start = DateTime.Today.AddDays(3).AddHours(10);
        var appointment = new Appointment
        {
            PetId = petId, VeterinarianId = vetId, ServiceId = service.Id, StartsAt = start,
            EndsAt = start.AddMinutes(service.DurationMinutes), DurationMinutes = service.DurationMinutes,
            Price = service.Price, CreatedAt = DateTime.Today,
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return new Setup(owner, vet, appointment.Id);
    }

    private async Task<AppointmentStatus> StatusAsync(int id)
    {
        await using var db = web.Database.NewContext();
        return (await db.Appointments.SingleAsync(a => a.Id == id)).Status;
    }

    [Fact]
    public async Task Vet_sees_request_in_panel_and_approves_it()
    {
        var s = await SetupAsync("a1");

        Assert.Contains("Hayvana1", await s.Vet.GetStringAsync("/Hekim"));
        var response = await WebFixture.PostFormAsync(s.Vet, $"/Appointments/Approve/{s.AppointmentId}", [], formPageUrl: "/Hekim");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(AppointmentStatus.Approved, await StatusAsync(s.AppointmentId));
        Assert.Contains("Onaylandı", await s.Owner.GetStringAsync($"/Appointments/Details/{s.AppointmentId}"));
    }

    [Fact]
    public async Task Vet_rejects_with_reason_and_owner_sees_it()
    {
        var s = await SetupAsync("r1");

        var response = await WebFixture.PostFormAsync(s.Vet, $"/Appointments/Reject/{s.AppointmentId}",
            new() { ["reason"] = "O gün izindeyim." }, formPageUrl: $"/Appointments/Details/{s.AppointmentId}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("O gün izindeyim.", await s.Owner.GetStringAsync("/Appointments"));
    }

    [Fact]
    public async Task Other_vet_and_member_get_404()
    {
        var s = await SetupAsync("x1");
        var other = await SetupAsync("x2");

        Assert.Equal(HttpStatusCode.NotFound, (await other.Vet.GetAsync($"/Appointments/Details/{s.AppointmentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Owner.GetAsync($"/Appointments/Details/{s.AppointmentId}")).StatusCode);
        var approve = await WebFixture.PostFormAsync(other.Vet, $"/Appointments/Approve/{s.AppointmentId}", [], formPageUrl: "/Hekim");
        var cancel = await WebFixture.PostFormAsync(other.Owner, $"/Appointments/Cancel/{s.AppointmentId}", [], formPageUrl: "/Appointments");
        var ownerApproves = await WebFixture.PostFormAsync(s.Owner, $"/Appointments/Approve/{s.AppointmentId}", [], formPageUrl: "/Appointments");

        Assert.Equal(HttpStatusCode.NotFound, approve.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ownerApproves.StatusCode);
        Assert.Equal(AppointmentStatus.Pending, await StatusAsync(s.AppointmentId));
    }

    [Fact]
    public async Task Owner_cancels_appointment()
    {
        var s = await SetupAsync("c1");

        var response = await WebFixture.PostFormAsync(s.Owner, $"/Appointments/Cancel/{s.AppointmentId}", [], formPageUrl: "/Appointments");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(AppointmentStatus.Cancelled, await StatusAsync(s.AppointmentId));
    }

    [Fact]
    public async Task Member_cannot_open_vet_panel()
    {
        var member = await web.MemberClientAsync("panelsiz@test.local");

        var response = await member.GetAsync("/Hekim");

        Assert.Contains("AccessDenied", response.Headers.Location!.ToString());
    }
}
