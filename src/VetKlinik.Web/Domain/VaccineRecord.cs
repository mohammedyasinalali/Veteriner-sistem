namespace VetKlinik.Web.Domain;

public class VaccineRecord
{
    public int Id { get; set; }
    public int PetId { get; set; }
    public Pet Pet { get; set; } = null!;
    public int? VeterinarianId { get; set; }
    public Veterinarian? Veterinarian { get; set; }
    public required string VaccineName { get; set; }
    public DateOnly AdministeredOn { get; set; }
    public DateOnly? NextDueOn { get; set; }
}
