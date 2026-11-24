using System.ComponentModel.DataAnnotations;

namespace VetKlinik.Web.Models.Admin;

public class VeterinarianFormModel
{
    [Display(Name = "Ad Soyad")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(100, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string FullName { get; set; } = "";

    [Display(Name = "E-posta")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    public string Email { get; set; } = "";

    /// <summary>Yalnızca yeni hekim oluşturulurken kullanılır.</summary>
    [Display(Name = "Parola")]
    [DataType(DataType.Password)]
    [MinLength(6, ErrorMessage = "{0} en az {1} karakter olmalıdır.")]
    public string? Password { get; set; }

    [Display(Name = "Unvan")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(50, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string Title { get; set; } = "Vet. Hek.";

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;

    public List<int> SpecialtyIds { get; set; } = [];
    public List<int> ServiceIds { get; set; } = [];
    public List<AvailabilityDayModel> Days { get; set; } = [];
}

public class AvailabilityDayModel
{
    public DayOfWeek Day { get; set; }
    public bool Works { get; set; }
    public TimeOnly? Starts { get; set; }
    public TimeOnly? Ends { get; set; }
}
