namespace VetKlinik.Web.Infrastructure;

/// <summary>
/// Kliniğin yerel saati (Europe/Istanbul). Sunucu hangi saat diliminde çalışırsa çalışsın
/// "şimdi" ve "bugün" buradan alınır; testlerde FakeTimeProvider verilir.
/// </summary>
public static class ClinicTime
{
    private static readonly TimeZoneInfo Istanbul = FindIstanbul();

    // Linux/macOS ve güncel Windows "Europe/Istanbul" adını tanır; eski Windows "Turkey Standard Time" kullanır.
    // Saat dilimi verisi olmayan ortamlarda sabit UTC+3 (Türkiye 2016'dan beri yaz saati uygulamıyor).
    private static TimeZoneInfo FindIstanbul()
    {
        foreach (var id in new[] { "Europe/Istanbul", "Turkey Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("Turkey", TimeSpan.FromHours(3), "Türkiye", "Türkiye");
    }

    public static DateTime Now(TimeProvider time) => TimeZoneInfo.ConvertTime(time.GetUtcNow(), Istanbul).DateTime;

    public static DateOnly Today(TimeProvider time) => DateOnly.FromDateTime(Now(time));
}
