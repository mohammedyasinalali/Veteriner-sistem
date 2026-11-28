using System.ComponentModel.DataAnnotations;
using VetKlinik.Web.Domain;
using VetKlinik.Web.Infrastructure;
using VetKlinik.Web.Models.Appointments;

namespace VetKlinik.Web.Api.Dtos;

/// <summary>
/// Randevu isteği. Bilerek yalnızca seçimleri içerir: süre, ücret ve durum sunucuda hizmetten
/// hesaplanır; gövdede gönderilse bile yok sayılır.
/// </summary>
public class CreateAppointmentDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Hayvan seçiniz.")]
    public int PetId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Hizmet seçiniz.")]
    public int ServiceId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Hekim seçiniz.")]
    public int VeterinarianId { get; set; }

    /// <summary>Klinik yerel saati, ör. "2026-12-07T10:00:00".</summary>
    [Required(ErrorMessage = "Saat seçiniz.")]
    public DateTime? StartsAt { get; set; }

    public BookingFormModel ToForm() => new()
    {
        PetId = PetId,
        ServiceId = ServiceId,
        VeterinarianId = VeterinarianId,
        StartsAt = StartsAt,
    };
}

public record AppointmentDto(
    int Id,
    int PetId, string PetName,
    int ServiceId, string ServiceName,
    int VeterinarianId, string VeterinarianName,
    DateTime StartsAt, DateTime EndsAt, int DurationMinutes, decimal Price,
    AppointmentStatus Status, string StatusName,
    string? RejectionReason, string? ExaminationNote)
{
    /// <summary>Pet, Service ve Veterinarian.User yüklenmiş olmalı.</summary>
    public static AppointmentDto From(Appointment a) => new(
        a.Id,
        a.PetId, a.Pet.Name,
        a.ServiceId, a.Service.Name,
        a.VeterinarianId, a.Veterinarian.User.FullName,
        a.StartsAt, a.EndsAt, a.DurationMinutes, a.Price,
        a.Status, AppointmentStatusNames.Name(a.Status),
        a.RejectionReason, a.ExaminationNote);
}
