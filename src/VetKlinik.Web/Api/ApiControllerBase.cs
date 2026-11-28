using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using VetKlinik.Web.Auth;
using VetKlinik.Web.Services;

namespace VetKlinik.Web.Api;

/// <summary>
/// REST API ortak tabanı. Kimlik doğrulama MVC ile aynı çerezdir; girişsiz istek 401 alır.
/// Hata gövdesi: ProblemDetails + "errors" (Türkçe mesaj listesi).
/// </summary>
[ApiController]
[Authorize]
public abstract class ApiControllerBase : ControllerBase
{
    protected CurrentUser Me => CurrentUser.From(User);

    /// <summary>Servis sonucunu HTTP'ye çevirir: bulunamadı → 404, diğer kural hataları → 400.</summary>
    protected ObjectResult Failure(ServiceResult result, string title) =>
        Error(result.IsNotFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest, title, result.Errors);

    protected ObjectResult Error(int status, string title, IEnumerable<string> errors) =>
        new(ApiProblems.Create(status, title, errors)) { StatusCode = status, ContentTypes = { ApiProblems.ContentType } };

    protected ObjectResult BadRequestError(string message) => Error(StatusCodes.Status400BadRequest, "İstek geçersiz.", [message]);

    protected ObjectResult NotFoundError(string message) => Error(StatusCodes.Status404NotFound, message, [message]);

    /// <summary>Model doğrulama hatalarını aynı biçime çevirir (Program.cs'te ApiBehaviorOptions'a bağlı).</summary>
    public static IActionResult InvalidModel(ActionContext context)
    {
        var errors = context.ModelState
            .SelectMany(e => e.Value!.Errors.Select(err => Message(e.Key, err)))
            .Distinct()
            .ToList();
        var problem = ApiProblems.Create(StatusCodes.Status400BadRequest, "İstek geçersiz.", errors);
        return new BadRequestObjectResult(problem) { ContentTypes = { ApiProblems.ContentType } };
    }

    // JSON çözümleme hataları İngilizce ve teknik gelir; kullanıcıya hangi alanın hatalı olduğu söylenir.
    // Sorgu parametresi hatalarının başına alan adı eklenir ("serviceId: 'abc' değeri geçerli değil.").
    private static string Message(string key, ModelError error)
    {
        if (error.Exception is not null || key.StartsWith('$'))
            return $"Geçersiz değer: {key.TrimStart('$', '.')}".TrimEnd(' ', ':');
        return string.IsNullOrEmpty(key) || key.Contains('.') || error.ErrorMessage.Contains(key, StringComparison.OrdinalIgnoreCase)
            ? error.ErrorMessage
            : $"{key}: {error.ErrorMessage}";
    }
}
