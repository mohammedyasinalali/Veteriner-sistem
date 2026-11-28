using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;
using VetKlinik.Web.Api;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Data;
using VetKlinik.Web.Infrastructure;
using VetKlinik.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddVetKlinikIdentity();
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection("Seed"));

builder.Services.AddVetKlinikServices();

builder.Services.AddControllersWithViews(options =>
{
    TurkishModelBindingMessages.Apply(options.ModelBindingMessageProvider);
    options.ModelBinderProviders.Insert(0, new TurkishDecimalModelBinderProvider());
})
.AddJsonOptions(o =>
{
    // REST API: Türkçe karakterler kaçışsız ("Kısırlaştırma"), enum'lar metin ("Pending").
    o.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
})
.ConfigureApiBehaviorOptions(o =>
{
    o.InvalidModelStateResponseFactory = ApiControllerBase.InvalidModel;
    // Gövdesiz 404/415 vb. İngilizce ProblemDetails yerine aşağıdaki /api durum kodu sayfasıyla doldurulur.
    o.SuppressMapClientErrors = true;
});

// Türkçe karakterler (ı, ş, ğ...) HTML'de &#x131; gibi kodlanmadan yazılsın.
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

var app = builder.Build();

// Açılışta bekleyen migration'lar uygulanır, roller ve Admin hesabı oluşturulur.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}
await DbSeeder.SeedAsync(app.Services);

// Configure the HTTP request pipeline.
// /api: her hata Türkçe problem+json (HTML hata sayfası veya boş gövde yok).
app.UseWhen(ApiProblems.IsApi, api =>
{
    api.UseExceptionHandler(handler => handler.Run(ApiProblems.WriteAsync));
    api.UseStatusCodePages(context => ApiProblems.WriteAsync(context.HttpContext));
});

if (!app.Environment.IsDevelopment())
{
    app.UseWhen(context => !ApiProblems.IsApi(context), web => web.UseExceptionHandler("/Home/Error"));
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Tüm istekler Türkçe kültürle işlenir: "499,99" ondalık, tarih ve para birimi biçimleri.
var turkish = new CultureInfo("tr-TR");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(turkish),
    SupportedCultures = [turkish],
    SupportedUICultures = [turkish],
});

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
