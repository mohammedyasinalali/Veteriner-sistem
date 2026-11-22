namespace VetKlinik.Web.Domain;

public class VetAvailability
{
    public int Id { get; set; }
    public int VeterinarianId { get; set; }
    public DayOfWeek Day { get; set; }
    public TimeOnly Starts { get; set; }
    public TimeOnly Ends { get; set; }
}
