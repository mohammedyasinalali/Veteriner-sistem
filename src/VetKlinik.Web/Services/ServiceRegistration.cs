using Microsoft.Extensions.DependencyInjection.Extensions;

namespace VetKlinik.Web.Services;

public static class ServiceRegistration
{
    /// <summary>İş kuralı servisleri: MVC, REST API ve MCP aynı kayıtları kullanır.</summary>
    public static IServiceCollection AddVetKlinikServices(this IServiceCollection services)
    {
        services.AddScoped<AccountService>();
        services.AddScoped<ServiceCatalogService>();
        services.AddScoped<ClinicService>();
        services.AddScoped<VeterinarianService>();
        services.AddScoped<SpecialtyService>();
        services.AddScoped<PetService>();
        services.AddScoped<VaccineService>();
        services.AddScoped<AppointmentService>();
        services.AddScoped<ReportService>();
        services.TryAddSingleton(TimeProvider.System);
        // Fotoğraflar wwwroot/uploads altına; ayarda "Uploads:Path" verilirse oraya kaydedilir.
        services.AddSingleton(sp => new PhotoStorage(
            sp.GetRequiredService<IConfiguration>()["Uploads:Path"]
            ?? Path.Combine(sp.GetRequiredService<IWebHostEnvironment>().WebRootPath, "uploads")));
        return services;
    }
}
