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
        return services;
    }
}
