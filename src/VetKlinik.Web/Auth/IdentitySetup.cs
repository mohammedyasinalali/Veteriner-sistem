using Microsoft.AspNetCore.Identity;
using VetKlinik.Web.Data;
using VetKlinik.Web.Domain;

namespace VetKlinik.Web.Auth;

public static class IdentitySetup
{
    /// <summary>Identity ayarları tek yerde: uygulama ve testler aynı kuralları kullanır.</summary>
    public static IServiceCollection AddVetKlinikIdentity(this IServiceCollection services)
    {
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 6;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<TurkishIdentityErrorDescriber>();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied";
        });

        return services;
    }
}
