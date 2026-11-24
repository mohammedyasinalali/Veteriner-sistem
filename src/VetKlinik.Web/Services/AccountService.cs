using Microsoft.AspNetCore.Identity;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Domain;
using VetKlinik.Web.Models.Account;

namespace VetKlinik.Web.Services;

public class AccountService(UserManager<ApplicationUser> userManager)
{
    /// <summary>Yeni üye oluşturur. Kayıt formundan gelen her kullanıcı yalnızca "Uye" rolünü alır.</summary>
    public async Task<ServiceResult> RegisterAsync(RegisterViewModel model)
    {
        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
        };

        var created = await userManager.CreateAsync(user, model.Password);
        if (!created.Succeeded)
            return ServiceResult.Fail([.. created.Errors.Select(e => e.Description)]);

        var role = await userManager.AddToRoleAsync(user, Roles.Uye);
        return role.Succeeded
            ? ServiceResult.Ok()
            : ServiceResult.Fail([.. role.Errors.Select(e => e.Description)]);
    }
}
