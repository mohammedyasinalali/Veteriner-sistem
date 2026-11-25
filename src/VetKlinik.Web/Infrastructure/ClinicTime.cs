namespace VetKlinik.Web.Infrastructure;

/// <summary>
/// Kliniğin yerel saati (Europe/Istanbul). Sunucu hangi saat diliminde çalışırsa çalışsın
/// "şimdi" ve "bugün" buradan alınır; testlerde FakeTimeProvider verilir.
/// </summary>
public static class ClinicTime
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public static DateTime Now(TimeProvider time) => TimeZoneInfo.ConvertTime(time.GetUtcNow(), Istanbul).DateTime;

    public static DateOnly Today(TimeProvider time) => DateOnly.FromDateTime(Now(time));
}
