using VetKlinik.Web.Domain;
using VetKlinik.Web.Services;

namespace VetKlinik.Web.Models.Appointments;

/// <summary>Randevu Al sayfası: arama seçimleri (GET) ve bulunan uygun saatler.</summary>
public class BookingPageModel
{
    public required IReadOnlyList<Pet> Pets { get; init; }
    public required IReadOnlyList<Service> Services { get; init; }
    public int? PetId { get; init; }
    public int? ServiceId { get; init; }
    public DateOnly? Date { get; init; }
    public required DateOnly Today { get; init; }
    public DateOnly LastDay => Today.AddDays(AppointmentService.MaxDaysAhead);

    /// <summary>Arama yapılmadıysa null; yapıldıysa uygun saati olan hekimler.</summary>
    public IReadOnlyList<VetSlots>? Results { get; init; }
}
