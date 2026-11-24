using System.Net;
using VetKlinik.Tests.Infrastructure;

namespace VetKlinik.Tests.Web;

public class AdminVeterinarianPagesTests(WebFixture web) : IClassFixture<WebFixture>
{
    [Fact]
    public async Task Admin_creates_veterinarian_who_can_log_in()
    {
        var admin = await web.AdminClientAsync();
        var create = await WebFixture.PostFormAsync(admin, "/Admin/Veterinarians/Create", new()
        {
            ["FullName"] = "Dr. Mehmet Demir",
            ["Email"] = "mehmet@test.local",
            ["Password"] = "hekim123",
            ["Title"] = "Vet. Hek.",
            ["IsActive"] = "true",
            ["Days[0].Day"] = "Monday",
            ["Days[0].Works"] = "true",
            ["Days[0].Starts"] = "09:00",
            ["Days[0].Ends"] = "12:00",
        });
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        Assert.Contains("Dr. Mehmet Demir", await admin.GetStringAsync("/Admin/Veterinarians"));

        var vet = web.NewClient();
        var login = await WebFixture.PostFormAsync(vet, "/Account/Login", new()
        {
            ["Email"] = "mehmet@test.local",
            ["Password"] = "hekim123",
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var adminPage = await vet.GetAsync("/Admin");
        Assert.Contains("AccessDenied", adminPage.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Edit_unknown_veterinarian_returns_404()
    {
        var admin = await web.AdminClientAsync();

        var response = await admin.GetAsync("/Admin/Veterinarians/Edit/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Admin_adds_specialty()
    {
        var admin = await web.AdminClientAsync();

        var response = await WebFixture.PostFormAsync(admin, "/Admin/Specialties/Create", new() { ["Name"] = "Göz Hastalıkları" },
            formPageUrl: "/Admin/Specialties");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Göz Hastalıkları", await admin.GetStringAsync("/Admin/Specialties"));
    }

    [Fact]
    public async Task Home_page_lists_active_veterinarians()
    {
        var admin = await web.AdminClientAsync();
        await WebFixture.PostFormAsync(admin, "/Admin/Veterinarians/Create", new()
        {
            ["FullName"] = "Dr. Elif Şahin",
            ["Email"] = "elif@test.local",
            ["Password"] = "hekim123",
            ["Title"] = "Vet. Hek.",
            ["IsActive"] = "true",
        });

        var html = await web.NewClient().GetStringAsync("/");

        Assert.Contains("Dr. Elif Şahin", html);
    }
}
