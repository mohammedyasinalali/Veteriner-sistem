namespace VetKlinik.Web.Auth;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Veteriner = "Veteriner";
    public const string Uye = "Uye";

    public static readonly string[] All = [Admin, Veteriner, Uye];
}
