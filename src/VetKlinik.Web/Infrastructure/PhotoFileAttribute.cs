using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using VetKlinik.Web.Services;

namespace VetKlinik.Web.Infrastructure;

/// <summary>
/// Fotoğraf alanı: uzantı (jpg/png) ve boyut (2 MB) kontrolü. Sunucuda çalışır ve
/// aynı kuralı tarayıcıya data-val-photo özellikleriyle aktarır (validation-tr.js).
/// İçerik imzası kontrolü ayrıca PhotoStorage içinde yapılır.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PhotoFileAttribute : ValidationAttribute, IClientModelValidator
{
    public PhotoFileAttribute() =>
        ErrorMessage = $"Fotoğraf jpg veya png olmalı ve {PhotoStorage.MaxBytes / 1024 / 1024} MB'ı geçmemelidir.";

    public override bool IsValid(object? value) =>
        value is not IFormFile file || (PhotoStorage.HasAllowedExtension(file.FileName) && file.Length <= PhotoStorage.MaxBytes);

    public void AddValidation(ClientModelValidationContext context)
    {
        context.Attributes["data-val"] = "true";
        context.Attributes["data-val-photo"] = ErrorMessage!;
        context.Attributes["data-val-photo-maxbytes"] = PhotoStorage.MaxBytes.ToString();
        context.Attributes["data-val-photo-extensions"] = string.Join(",", PhotoStorage.Extensions);
    }
}
