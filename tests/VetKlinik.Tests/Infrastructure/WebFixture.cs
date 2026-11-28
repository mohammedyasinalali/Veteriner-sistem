using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace VetKlinik.Tests.Infrastructure;

/// <summary>Gerçek uygulamayı geçici bir test veritabanıyla ayağa kaldırır (test sınıfı başına bir kez).</summary>
public sealed partial class WebFixture : IAsyncLifetime
{
    public SqlServerFixture Database { get; } = new();
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b
                .UseSetting("ConnectionStrings:Default", Database.ConnectionString)
                .UseSetting("Uploads:Path", UploadsPath));
    }

    public string UploadsPath { get; } = Path.Combine(Path.GetTempPath(), $"vetklinik-uploads-{Guid.NewGuid():N}");

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Database.DisposeAsync();
        if (Directory.Exists(UploadsPath))
            Directory.Delete(UploadsPath, recursive: true);
    }

    public HttpClient NewClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public Task<HttpClient> AdminClientAsync() => LoginClientAsync("g231210566@sakarya.edu.tr", "sau");

    /// <summary>Var olan bir hesapla giriş yapmış istemci (ör. TestData kullanıcıları, parola TestData.Password).</summary>
    public async Task<HttpClient> LoginClientAsync(string email, string password)
    {
        var client = NewClient();
        var response = await PostFormAsync(client, "/Account/Login", new()
        {
            ["Email"] = email,
            ["Password"] = password,
        });
        if ((int)response.StatusCode != 302)
            throw new InvalidOperationException($"{email} girişi başarısız.");
        return client;
    }

    /// <summary>Yeni bir üye kaydeder; kayıt sonrası otomatik giriş yapılmış istemciyi döner.</summary>
    public async Task<HttpClient> MemberClientAsync(string email)
    {
        var client = NewClient();
        var response = await PostFormAsync(client, "/Account/Register", new()
        {
            ["FullName"] = "Test Üye",
            ["Email"] = email,
            ["Password"] = "uye1234",
            ["ConfirmPassword"] = "uye1234",
        });
        if ((int)response.StatusCode != 302)
            throw new InvalidOperationException("Üye kaydı başarısız.");
        return client;
    }

    /// <summary>Formun bulunduğu sayfadan antiforgery token'ı okuyup formu gönderir.</summary>
    public static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client, string url, Dictionary<string, string> fields, string? formPageUrl = null)
    {
        var page = await client.GetStringAsync(formPageUrl ?? url);
        fields["__RequestVerificationToken"] = TokenRegex().Match(page).Groups[1].Value;
        return await client.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}
