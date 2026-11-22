namespace VetKlinik.Web.Domain;

public class Service
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Veterinarian> Veterinarians { get; set; } = [];
}
