using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace VetKlinik.Web.Api;

/// <summary>
/// API sorgu tarihleri yalnız yyyy-MM-dd kabul eder. Varsayılan bağlayıcı "01.11.2026"yı ay.gün olarak
/// okuyup sessizce 11 Ocak yapıyordu; burada hata döner.
/// </summary>
public class IsoDateBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        var raw = context.ValueProvider.GetValue(context.ModelName).FirstValue;
        if (string.IsNullOrWhiteSpace(raw))
            return Task.CompletedTask; // verilmedi: null kalır, [Required] varsa o yakalar

        if (DateOnly.TryParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            context.Result = ModelBindingResult.Success(date);
        else
            context.ModelState.TryAddModelError(context.ModelName, "Tarih yyyy-MM-dd biçiminde olmalıdır (ör. 2026-12-07).");
        return Task.CompletedTask;
    }
}
