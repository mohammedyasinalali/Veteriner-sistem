using System.ComponentModel.DataAnnotations;

namespace VetKlinik.Web.Models.Admin;

public class ServiceFormModel
{
    [Display(Name = "Hizmet adı")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(100, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string Name { get; set; } = "";

    [Display(Name = "Açıklama")]
    [StringLength(500, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string? Description { get; set; }

    [Display(Name = "Süre (dakika)")]
    [Range(10, 480, ErrorMessage = "{0} {1} ile {2} arasında olmalıdır.")]
    public int DurationMinutes { get; set; } = 30;

    [Display(Name = "Ücret (₺)")]
    [Range(typeof(decimal), "0", "100000", ErrorMessage = "{0} {1} ile {2} arasında olmalıdır.")]
    public decimal Price { get; set; }

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;
}
