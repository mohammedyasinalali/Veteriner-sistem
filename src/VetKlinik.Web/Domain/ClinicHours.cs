namespace VetKlinik.Web.Domain;

public class ClinicHours
{
    public int Id { get; set; }
    public int ClinicId { get; set; }
    public DayOfWeek Day { get; set; }
    public bool IsClosed { get; set; }
    public TimeOnly Opens { get; set; }
    public TimeOnly Closes { get; set; }
}
