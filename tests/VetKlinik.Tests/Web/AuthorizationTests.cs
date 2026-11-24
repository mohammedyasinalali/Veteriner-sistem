using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using VetKlinik.Tests.Infrastructure;

namespace VetKlinik.Tests.Web;

public class AuthorizationTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private HttpClient NewClient() => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Default", fixture.ConnectionString))
        .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Anonymous_admin_request_redirects_to_login()
    {
        var response = await NewClient().GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Login_page_renders()
    {
        var response = await NewClient().GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Turkish_characters_are_not_html_encoded()
    {
        var html = await NewClient().GetStringAsync("/Account/Login");

        Assert.Contains("Beni hatırla", html); // Display adı tag helper ile yazılır
    }
}
