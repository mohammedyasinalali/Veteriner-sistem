using Microsoft.AspNetCore.Http;
using VetKlinik.Web.Services;

namespace VetKlinik.Tests.Services;

public class PhotoStorageTests : IDisposable
{
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"vetklinik-photos-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
    }

    private static IFormFile File(string name, byte[] content) =>
        new FormFile(new MemoryStream(content), 0, content.Length, "Photo", name);

    [Fact]
    public async Task Accepts_real_png_and_saves_with_guid_name()
    {
        var storage = new PhotoStorage(folder);
        var file = File("kedi.png", [.. PngHeader, 1, 2, 3]);

        Assert.True(storage.Validate(file).Succeeded);
        var saved = await storage.SaveAsync(file);

        Assert.EndsWith(".png", saved);
        Assert.DoesNotContain("kedi", saved);
        Assert.True(System.IO.File.Exists(Path.Combine(folder, saved)));
    }

    [Fact]
    public void Accepts_jpeg()
    {
        var result = new PhotoStorage(folder).Validate(File("kopek.JPG", [0xFF, 0xD8, 0xFF, 0xE0, 0, 0]));

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
    }

    [Fact]
    public void Rejects_text_renamed_to_png()
    {
        var result = new PhotoStorage(folder).Validate(File("sahte.png", "merhaba dunya"u8.ToArray()));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Rejects_file_over_2mb()
    {
        var big = new byte[2 * 1024 * 1024 + 1];
        PngHeader.CopyTo(big, 0);

        var result = new PhotoStorage(folder).Validate(File("buyuk.png", big));

        Assert.False(result.Succeeded);
        Assert.Contains("2 MB", string.Join(" ", result.Errors));
    }

    [Fact]
    public void Rejects_gif_extension()
    {
        var result = new PhotoStorage(folder).Validate(File("anim.gif", [0x47, 0x49, 0x46, 0x38]));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Delete_ignores_path_traversal()
    {
        Directory.CreateDirectory(folder);
        var outside = Path.Combine(Path.GetTempPath(), $"dokunma-{Guid.NewGuid():N}.txt");
        System.IO.File.WriteAllText(outside, "x");
        try
        {
            new PhotoStorage(folder).Delete("../" + Path.GetFileName(outside));
            Assert.True(System.IO.File.Exists(outside));
        }
        finally
        {
            System.IO.File.Delete(outside);
        }
    }
}
