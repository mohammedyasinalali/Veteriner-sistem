using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Data;
using VetKlinik.Web.Infrastructure;
using VetKlinik.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddVetKlinikIdentity();
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection("Seed"));

builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<ServiceCatalogService>();
builder.Services.AddScoped<ClinicService>();

builder.Services.AddControllersWithViews(options =>
    TurkishModelBindingMessages.Apply(options.ModelBindingMessageProvider));

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
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
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
