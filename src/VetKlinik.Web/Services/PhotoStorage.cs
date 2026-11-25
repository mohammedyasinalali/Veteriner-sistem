namespace VetKlinik.Web.Services;

/// <summary>
/// Hayvan fotoğraflarını diske kaydeder. Dosya adı kullanıcıdan alınmaz (GUID), uzantı ve
/// ilk baytlar (dosya imzası) kontrol edilir; böylece adı ".png" yapılmış başka bir dosya kabul edilmez.
/// </summary>
public class PhotoStorage(string folder)
{
    public const long MaxBytes = 2 * 1024 * 1024;
    public static readonly string[] Extensions = [".jpg", ".jpeg", ".png"];

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool HasAllowedExtension(string fileName) =>
        Extensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());

    public ServiceResult Validate(IFormFile file)
    {
        if (file.Length == 0)
            return ServiceResult.Fail("Fotoğraf dosyası boş.");
        if (file.Length > MaxBytes)
            return ServiceResult.Fail("Fotoğraf en fazla 2 MB olabilir.");
        if (!HasAllowedExtension(file.FileName))
            return ServiceResult.Fail("Fotoğraf yalnızca jpg veya png olabilir.");

        var header = new byte[PngSignature.Length];
        using (var stream = file.OpenReadStream())
            stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);

        var isPng = Path.GetExtension(file.FileName).Equals(".png", StringComparison.OrdinalIgnoreCase);
        var signature = isPng ? PngSignature : JpegSignature;
        return header.AsSpan().StartsWith(signature)
            ? ServiceResult.Ok()
            : ServiceResult.Fail("Dosyanın içeriği geçerli bir jpg/png resmi değil.");
    }

    /// <summary>Önce <see cref="Validate"/> çağrılmalıdır. Kaydedilen dosya adını döner.</summary>
    public async Task<string> SaveAsync(IFormFile file)
    {
        Directory.CreateDirectory(folder);
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant() == ".png" ? ".png" : ".jpg";
        var fileName = $"{Guid.NewGuid():N}{extension}";
        await using var target = File.Create(Path.Combine(folder, fileName));
        await file.CopyToAsync(target);
        return fileName;
    }

    /// <summary>Kayıtlı dosyanın tam yolu; yoksa null.</summary>
    public string? PathOf(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return null;
        var path = Path.Combine(folder, Path.GetFileName(fileName));
        return File.Exists(path) ? path : null;
    }

    public void Delete(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return;
        // Yalnızca klasördeki dosya adı kullanılır; "../" gibi yollar dışarı çıkamaz.
        var path = Path.Combine(folder, Path.GetFileName(fileName));
        if (File.Exists(path))
            File.Delete(path);
    }
}
