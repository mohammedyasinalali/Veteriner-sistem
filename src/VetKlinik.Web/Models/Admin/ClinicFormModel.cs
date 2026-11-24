using System.ComponentModel.DataAnnotations;

namespace VetKlinik.Web.Models.Admin;

public class ClinicFormModel
{
    [Display(Name = "Klinik adı")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(100, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string Name { get; set; } = "";

    [Display(Name = "Adres")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(300, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string Address { get; set; } = "";

    [Display(Name = "Telefon")]
    [Required(ErrorMessage = "{0} zorunludur.")]
    [StringLength(30, ErrorMessage = "{0} en fazla {1} karakter olabilir.")]
    public string Phone { get; set; } = "";

    public List<ClinicDayModel> Days { get; set; } = [];
}

public class ClinicDayModel
{
    public DayOfWeek Day { get; set; }

    [Display(Name = "Kapalı")]
    public bool IsClosed { get; set; }

    [Display(Name = "Açılış")]
    public TimeOnly Opens { get; set; }

    [Display(Name = "Kapanış")]
    public TimeOnly Closes { get; set; }
}
