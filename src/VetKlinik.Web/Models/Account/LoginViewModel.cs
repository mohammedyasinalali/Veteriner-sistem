using System.ComponentModel.DataAnnotations;

namespace VetKlinik.Web.Models.Account;

public class LoginViewModel
{
    [Display(Name = "E-posta")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    public string Email { get; set; } = "";

    [Display(Name = "Parola")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "Beni hatırla")]
    public bool RememberMe { get; set; }
}
