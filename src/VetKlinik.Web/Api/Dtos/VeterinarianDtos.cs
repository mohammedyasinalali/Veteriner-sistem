using VetKlinik.Web.Domain;
using VetKlinik.Web.Services;

namespace VetKlinik.Web.Api.Dtos;

public record ServiceSummaryDto(int Id, string Name, int DurationMinutes, decimal Price)
{
    public static ServiceSummaryDto From(Service s) => new(s.Id, s.Name, s.DurationMinutes, s.Price);
}

/// <summary>Hekim: yalnızca herkese açık bilgiler (e-posta, telefon, hesap bilgisi yok).</summary>
public record VeterinarianDto(
    int Id, string FullName, string Title, IReadOnlyList<string> Specialties, IReadOnlyList<ServiceSummaryDto> Services)
{
    public static VeterinarianDto From(Veterinarian v) => new(
        v.Id,
        v.User.FullName,
        v.Title,
        v.Specialties.Select(s => s.Name).Order().ToList(),
        v.Services.Where(s => s.IsActive).OrderBy(s => s.Name).Select(ServiceSummaryDto.From).ToList());
}

public record VetSlotsDto(int VeterinarianId, string FullName, string Title, IReadOnlyList<DateTime> Slots)
{
    public static VetSlotsDto From(VetSlots v) => new(v.VeterinarianId, v.FullName, v.Title, v.Slots);
}

/// <summary>Bir hizmet ve gün için uygun saatler. Saatler klinik yerel saatidir (Europe/Istanbul).</summary>
public record AvailabilityDto(
    int ServiceId, string ServiceName, int DurationMinutes, DateOnly Date, IReadOnlyList<VetSlotsDto> Veterinarians);
