using System.ComponentModel.DataAnnotations;

namespace VetKlinik.Web.Models.Account;

public class RegisterViewModel
{
    [Display(Name = "Ad Soyad")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(100, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string FullName { get; set; } = "";

    [Display(Name = "E-posta")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    public string Email { get; set; } = "";

    [Display(Name = "Parola")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [DataType(DataType.Password)]
    [MinLength(6, ErrorMessage = "{0} en az {1} karakter olmalıdır.")]
    [RegularExpression(@".*\d.*", ErrorMessage = "Parola en az bir rakam içermelidir.")]
    public string Password { get; set; } = "";

    [Display(Name = "Parola (tekrar)")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Parolalar eşleşmiyor.")]
    public string ConfirmPassword { get; set; } = "";
}
