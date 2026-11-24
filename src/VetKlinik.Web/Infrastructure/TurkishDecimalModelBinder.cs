using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace VetKlinik.Web.Infrastructure;

/// <summary>
/// Ondalık sayıları yalnızca virgülle kabul eder ("499,99").
/// Varsayılan bağlayıcı tr-TR'de noktayı binlik ayırıcı sayar ve "499.99" değerini sessizce 49999 yapar.
/// </summary>
public class TurkishDecimalModelBinder : IModelBinder
{
    private static readonly CultureInfo Turkish = new("tr-TR");

    public Task BindModelAsync(ModelBindingContext context)
    {
        var value = context.ValueProvider.GetValue(context.ModelName);
        if (value == ValueProviderResult.None)
            return Task.CompletedTask;

        context.ModelState.SetModelValue(context.ModelName, value);
        var text = value.FirstValue?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            if (Nullable.GetUnderlyingType(context.ModelType) is not null)
                context.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        if (!text.Contains('.') &&
            decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Turkish, out var number))
        {
            context.Result = ModelBindingResult.Success(number);
            return Task.CompletedTask;
        }

        context.ModelState.TryAddModelError(context.ModelName,
            $"{context.ModelMetadata.GetDisplayName()} geçerli bir sayı olmalıdır. Ondalık ayırıcı olarak virgül kullanın (ör. 499,99).");
        return Task.CompletedTask;
    }
}

public class TurkishDecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context) =>
        context.Metadata.UnderlyingOrModelType == typeof(decimal) ? new TurkishDecimalModelBinder() : null;
}
