namespace VetKlinik.Web.Services;

/// <summary>Servislerin ortak sonucu: başarılı mı, değilse Türkçe hata mesajları.</summary>
public record ServiceResult(bool Succeeded, IReadOnlyList<string> Errors)
{
    public static ServiceResult Ok() => new(true, []);
    public static ServiceResult Fail(params string[] errors) => new(false, errors);
}

public record ServiceResult<T>(bool Succeeded, IReadOnlyList<string> Errors, T? Value)
    : ServiceResult(Succeeded, Errors)
{
    public static ServiceResult<T> Ok(T value) => new(true, [], value);
    public static new ServiceResult<T> Fail(params string[] errors) => new(false, errors, default);
}
