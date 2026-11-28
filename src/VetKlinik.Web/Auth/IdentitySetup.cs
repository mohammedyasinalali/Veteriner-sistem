using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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

            // REST API istemcisi HTML giriş sayfasına yönlendirilmez: 401 (giriş yok) / 403 (yetki yok).
            options.Events.OnRedirectToLogin = context => ApiAwareRedirect(context, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = context => ApiAwareRedirect(context, StatusCodes.Status403Forbidden);
        });

        return services;
    }

    private static Task ApiAwareRedirect(RedirectContext<CookieAuthenticationOptions> context, int apiStatus)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
            context.Response.StatusCode = apiStatus;
        else
            context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }
}
