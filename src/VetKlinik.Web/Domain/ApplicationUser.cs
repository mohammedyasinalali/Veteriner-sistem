using Microsoft.AspNetCore.Identity;

namespace VetKlinik.Web.Domain;

public class ApplicationUser : IdentityUser
{
    public required string FullName { get; set; }
}
