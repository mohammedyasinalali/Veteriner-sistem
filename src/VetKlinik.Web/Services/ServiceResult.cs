namespace VetKlinik.Web.Services;

/// <summary>
/// Servislerin ortak sonucu: başarılı mı, değilse Türkçe hata mesajları.
/// <see cref="IsNotFound"/>: kayıt yok ya da kullanıcı göremiyor/yetkisi yok — MVC ve API bunu 404'e çevirir,
/// diğer hatalar kural ihlalidir (sayfada mesaj / API'de 400).
/// </summary>
public record ServiceResult(bool Succeeded, IReadOnlyList<string> Errors)
{
    public bool IsNotFound { get; init; }

    public static ServiceResult Ok() => new(true, []);
    public static ServiceResult Fail(params string[] errors) => new(false, errors);
    public static ServiceResult NotFound(string message) => new(false, [message]) { IsNotFound = true };
}

public record ServiceResult<T>(bool Succeeded, IReadOnlyList<string> Errors, T? Value)
    : ServiceResult(Succeeded, Errors)
{
    public static ServiceResult<T> Ok(T value) => new(true, [], value);
    public static new ServiceResult<T> Fail(params string[] errors) => new(false, errors, default);
    public static new ServiceResult<T> NotFound(string message) => new(false, [message], default) { IsNotFound = true };
}
