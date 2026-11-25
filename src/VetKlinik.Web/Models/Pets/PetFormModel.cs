using System.ComponentModel.DataAnnotations;
using VetKlinik.Web.Domain;
using VetKlinik.Web.Infrastructure;

namespace VetKlinik.Web.Models.Pets;

/// <summary>Hayvan ekleme/düzenleme formu. Sahip alanı bilerek yok: sahip her zaman giriş yapan kullanıcıdır.</summary>
public class PetFormModel
{
    [Display(Name = "Adı")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(50, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string Name { get; set; } = "";

    [Display(Name = "Türü")]
    [EnumDataType(typeof(Species), ErrorMessage = "Geçerli bir tür seçiniz.")]
    public Species Species { get; set; } = Species.Dog;

    [Display(Name = "Irkı")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(50, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string Breed { get; set; } = "";

    [Display(Name = "Doğum tarihi")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    public DateOnly BirthDate { get; set; }

    [Display(Name = "Kilo (kg)")]
    [Range(typeof(decimal), "0.01", "500", ParseLimitsInInvariantCulture = true,
        ErrorMessage = "{0} 0,01 ile 500 arasında olmalıdır.")]
    public decimal WeightKg { get; set; }

    [Display(Name = "Fotoğraf")]
    [PhotoFile]
    public IFormFile? Photo { get; set; }

    [Display(Name = "Mevcut fotoğrafı kaldır")]
    public bool RemovePhoto { get; set; }
}
