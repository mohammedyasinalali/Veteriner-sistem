namespace VetKlinik.Web.Domain;

public class Veterinarian
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public required string Title { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Specialty> Specialties { get; set; } = [];
    public List<Service> Services { get; set; } = [];
    public List<VetAvailability> Availabilities { get; set; } = [];
}
