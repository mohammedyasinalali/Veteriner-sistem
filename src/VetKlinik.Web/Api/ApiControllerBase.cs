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
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected CurrentUser Me => CurrentUser.From(User);

    /// <summary>Servis sonucunu HTTP'ye çevirir: bulunamadı → 404, diğer kural hataları → 400.</summary>
    protected ObjectResult Failure(ServiceResult result, string title) =>
        Error(result.IsNotFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest, title, result.Errors);

    protected ObjectResult Error(int status, string title, IEnumerable<string> errors)
    {
        var problem = new ProblemDetails { Status = status, Title = title };
        problem.Extensions["errors"] = errors.ToList();
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }

    protected ObjectResult NotFoundError(string message) => Error(StatusCodes.Status404NotFound, message, [message]);

    /// <summary>Model doğrulama hatalarını aynı biçime çevirir (Program.cs'te ApiBehaviorOptions'a bağlı).</summary>
    public static IActionResult InvalidModel(ActionContext context)
    {
        var errors = context.ModelState
            .SelectMany(e => e.Value!.Errors.Select(err => Message(e.Key, err)))
            .Distinct()
            .ToList();
        var problem = new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "İstek geçersiz." };
        problem.Extensions["errors"] = errors;
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    // JSON çözümleme hataları İngilizce ve teknik gelir; kullanıcıya hangi alanın hatalı olduğu söylenir.
    private static string Message(string key, ModelError error) =>
        error.Exception is not null || key.StartsWith('$')
            ? $"Geçersiz değer: {key.TrimStart('$', '.')}".TrimEnd(' ', ':')
            : error.ErrorMessage;
}
