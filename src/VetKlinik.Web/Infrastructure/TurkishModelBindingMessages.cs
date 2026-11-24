using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace VetKlinik.Web.Infrastructure;

/// <summary>MVC'nin yerleşik doğrulama mesajları (sayı değil, boş bırakılamaz...) Türkçe olsun.</summary>
public static class TurkishModelBindingMessages
{
    public static void Apply(DefaultModelBindingMessageProvider p)
    {
        p.SetValueMustBeANumberAccessor(field => $"{field} geçerli bir sayı olmalıdır.");
        p.SetAttemptedValueIsInvalidAccessor((value, field) => $"'{value}' değeri {field} için geçerli değil.");
        p.SetUnknownValueIsInvalidAccessor(field => $"{field} için girilen değer geçerli değil.");
        p.SetValueIsInvalidAccessor(value => $"'{value}' değeri geçerli değil.");
        p.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"'{value}' değeri geçerli değil.");
        p.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Girilen değer geçerli değil.");
        p.SetNonPropertyValueMustBeANumberAccessor(() => "Alan geçerli bir sayı olmalıdır.");
        p.SetValueMustNotBeNullAccessor(value => $"'{value}' değeri boş bırakılamaz.");
        p.SetMissingBindRequiredValueAccessor(field => $"{field} için değer girilmelidir.");
        p.SetMissingKeyOrValueAccessor(() => "Değer girilmelidir.");
        p.SetMissingRequestBodyRequiredValueAccessor(() => "İstek gövdesi boş olamaz.");
    }
}
