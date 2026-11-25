using VetKlinik.Web.Domain;

namespace VetKlinik.Web.Infrastructure;

/// <summary>Randevu durumlarının Türkçe adları ve Bootstrap rozet renkleri.</summary>
public static class AppointmentStatusNames
{
    public static string Name(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Pending => "Onay bekliyor",
        AppointmentStatus.Approved => "Onaylandı",
        AppointmentStatus.Rejected => "Reddedildi",
        AppointmentStatus.Completed => "Tamamlandı",
        _ => "İptal edildi",
    };

    public static string Badge(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Pending => "text-bg-warning",
        AppointmentStatus.Approved => "text-bg-success",
        AppointmentStatus.Rejected => "text-bg-danger",
        AppointmentStatus.Completed => "text-bg-primary",
        _ => "text-bg-secondary",
    };
}
