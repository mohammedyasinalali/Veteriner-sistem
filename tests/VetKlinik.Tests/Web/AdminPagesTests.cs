using System.Net;
using VetKlinik.Tests.Infrastructure;

namespace VetKlinik.Tests.Web;

public class AdminPagesTests(WebFixture web) : IClassFixture<WebFixture>
{
    [Fact]
    public async Task Anonymous_services_page_redirects_to_login()
    {
        var response = await web.NewClient().GetAsync("/Admin/Services");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Admin_sees_seeded_services()
    {
        var client = await web.AdminClientAsync();

        var html = await client.GetStringAsync("/Admin/Services");

        Assert.Contains("Kısırlaştırma", html);
    }

    [Fact]
    public async Task Admin_creates_service_with_turkish_decimal()
    {
        var client = await web.AdminClientAsync();

        var response = await WebFixture.PostFormAsync(client, "/Admin/Services/Create", new()
        {
            ["Name"] = "Mikroçip Takma",
            ["DurationMinutes"] = "15",
            ["Price"] = "499,99",
            ["IsActive"] = "true",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var html = await client.GetStringAsync("/Admin/Services");
        Assert.Contains("Mikroçip Takma", html);
        Assert.Contains("499,99", html);
    }

    [Fact]
    public async Task Edit_unknown_service_returns_404()
    {
        var client = await web.AdminClientAsync();

        var response = await client.GetAsync("/Admin/Services/Edit/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Home_page_shows_active_services_and_hours()
    {
        var html = await web.NewClient().GetStringAsync("/");

        Assert.Contains("Muayene", html);
        Assert.Contains("Pazartesi", html);
    }

    [Fact]
    public async Task Invalid_number_message_is_turkish()
    {
        var client = await web.AdminClientAsync();

        var response = await WebFixture.PostFormAsync(client, "/Admin/Services/Create", new()
        {
            ["Name"] = "Deneme",
            ["DurationMinutes"] = "15",
            ["Price"] = "abc",
        });
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("geçerli bir sayı", html);
        Assert.DoesNotContain("must be a number", html);
    }
}
