using System.ComponentModel.DataAnnotations;

namespace VetKlinik.Web.Models.Pets;

/// <summary>Aşı kaydı formu. Hayvan id'si formda değil, adres satırında taşınır ve yetkisi servis tarafından kontrol edilir.</summary>
public class VaccineFormModel
{
    [Display(Name = "Aşı adı")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(100, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string VaccineName { get; set; } = "";

    [Display(Name = "Uygulama tarihi")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    public DateOnly AdministeredOn { get; set; }

    [Display(Name = "Sonraki doz tarihi")]
    public DateOnly? NextDueOn { get; set; }
}
