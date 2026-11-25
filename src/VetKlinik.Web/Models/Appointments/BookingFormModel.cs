using System.ComponentModel.DataAnnotations;

namespace VetKlinik.Web.Models.Appointments;

/// <summary>
/// Randevu isteği. Bilerek yalnızca seçimleri içerir: süre, ücret, bitiş ve durum
/// sunucuda hizmetten hesaplanır, formdan gelse bile yok sayılır.
/// </summary>
public class BookingFormModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Hayvan seçiniz.")]
    public int PetId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Hizmet seçiniz.")]
    public int ServiceId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Hekim seçiniz.")]
    public int VeterinarianId { get; set; }

    [Required(ErrorMessage = "Saat seçiniz.")]
    public DateTime StartsAt { get; set; }
}
