using Microsoft.AspNetCore.Mvc;

namespace VetKlinik.Web.Api;

/// <summary>
/// /api altındaki tüm hatalar tek biçimde döner: application/problem+json, Türkçe "title" ve "errors" listesi.
/// Gövdesi olmayan durum kodları (401, 403, bilinmeyen yol 404, 415, 500) buradan doldurulur.
/// </summary>
public static class ApiProblems
{
    public const string ContentType = "application/problem+json";

    public static bool IsApi(HttpContext context) => context.Request.Path.StartsWithSegments("/api");

    public static string Title(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "İstek geçersiz.",
        StatusCodes.Status401Unauthorized => "Giriş yapmanız gerekiyor.",
        StatusCodes.Status403Forbidden => "Bu işlem için yetkiniz yok.",
        StatusCodes.Status404NotFound => "İstenen kayıt veya adres bulunamadı.",
        StatusCodes.Status405MethodNotAllowed => "Bu adres bu HTTP yöntemini desteklemiyor.",
        StatusCodes.Status415UnsupportedMediaType => "İstek gövdesi JSON olmalıdır (Content-Type: application/json).",
        >= 500 => "Beklenmeyen bir hata oluştu.",
        _ => "İstek tamamlanamadı.",
    };

    public static ProblemDetails Create(int status, string title, IEnumerable<string> errors)
    {
        var problem = new ProblemDetails { Status = status, Title = title };
        problem.Extensions["errors"] = errors.ToList();
        return problem;
    }

    /// <summary>Henüz yazılmamış bir yanıta durum koduna uygun gövdeyi yazar.</summary>
    public static Task WriteAsync(HttpContext context)
    {
        var status = context.Response.StatusCode;
        var title = Title(status);
        return context.Response.WriteAsJsonAsync(Create(status, title, [title]), options: null, contentType: ContentType);
    }
}
