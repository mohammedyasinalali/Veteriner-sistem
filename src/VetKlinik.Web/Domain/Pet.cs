namespace VetKlinik.Web.Domain;

public class Pet
{
    public int Id { get; set; }
    public string OwnerId { get; set; } = null!;
    public ApplicationUser Owner { get; set; } = null!;
    public required string Name { get; set; }
    public Species Species { get; set; }
    public required string Breed { get; set; }
    public DateOnly BirthDate { get; set; }
    public decimal WeightKg { get; set; }
    public string? PhotoFileName { get; set; }
    public List<VaccineRecord> VaccineRecords { get; set; } = [];
    public List<Appointment> Appointments { get; set; } = [];
}
