namespace VetKlinik.Web.Domain;

public class Appointment
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public Pet Pet { get; set; } = null!;
    public int VeterinarianId { get; set; }
    public Veterinarian Veterinarian { get; set; } = null!;
    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    // Süre ve ücret randevu anındaki değerlerdir; hizmet sonradan değişse de randevu değişmez.
    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public string? RejectionReason { get; set; }
    public string? ExaminationNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
