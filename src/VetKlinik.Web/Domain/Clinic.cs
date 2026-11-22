namespace VetKlinik.Web.Domain;

public class Clinic
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Address { get; set; }
    public required string Phone { get; set; }
    public List<ClinicHours> Hours { get; set; } = [];
}
