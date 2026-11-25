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

    /// <summary>
    /// Dışarıdan gelen zamanı klinik saatine çevirir. UTC ("...Z") ve yerel saat (sunucu) dönüştürülür;
    /// türü belirtilmemiş zaman zaten klinik saati kabul edilir.
    /// </summary>
    public static DateTime ToClinic(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => TimeZoneInfo.ConvertTimeFromUtc(value, Istanbul),
        DateTimeKind.Local => TimeZoneInfo.ConvertTimeFromUtc(value.ToUniversalTime(), Istanbul),
        _ => value,
    };
}
