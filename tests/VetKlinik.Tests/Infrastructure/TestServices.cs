using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Data;
using VetKlinik.Web.Services;

namespace VetKlinik.Tests.Infrastructure;

/// <summary>Uygulamadaki Identity ayarlarıyla, test veritabanına bağlı bir DI kapsayıcısı kurar.</summary>
public static class TestServices
{
    public static ServiceProvider Build(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:AdminEmail"] = "g231210566@sakarya.edu.tr",
                ["Seed:AdminPassword"] = "sau",
                ["Seed:AdminFullName"] = "Sistem Yöneticisi",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<SeedOptions>(configuration.GetSection("Seed"));
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));
        services.AddVetKlinikIdentity();
        services.AddVetKlinikServices();
        return services.BuildServiceProvider();
    }
}
