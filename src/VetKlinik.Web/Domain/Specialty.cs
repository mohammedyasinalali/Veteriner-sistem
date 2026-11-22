namespace VetKlinik.Web.Domain;

public class Specialty
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public List<Veterinarian> Veterinarians { get; set; } = [];
}
