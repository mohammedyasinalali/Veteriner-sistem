using System.Security.Claims;

namespace VetKlinik.Web.Auth;

/// <summary>
/// İşlemi yapan kullanıcı. Servisler yetki kararını bununla verir; MVC, API ve MCP
/// kendi kimlik bilgisinden bu nesneyi üretip servise geçirir.
/// </summary>
public sealed record CurrentUser(string Id, bool IsAdmin, bool IsVeterinarian)
{
    public static CurrentUser From(ClaimsPrincipal principal) => new(
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Giriş yapmış kullanıcı bekleniyordu."),
        principal.IsInRole(Roles.Admin),
        principal.IsInRole(Roles.Veteriner));
}
