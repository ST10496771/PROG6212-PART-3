namespace RaceDayApi.Services;

/// <summary>Validates uploaded images: size, extension and the real file signature (not just the client-supplied type).</summary>
public static class ImageUpload
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> ExtensionToType = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp"
    };

    /// <returns>An error message, or null when the file is a valid image.</returns>
    public static string? Validate(IFormFile? file, out string contentType)
    {
        contentType = string.Empty;

        if (file == null || file.Length == 0) return "Please choose an image file.";
        if (file.Length > MaxBytes) return "The image must be 5 MB or smaller.";

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!ExtensionToType.TryGetValue(extension, out var expectedType))
            return "Only JPG, PNG, GIF or WEBP images are allowed.";

        var header = new byte[12];
        int read;
        using (var stream = file.OpenReadStream())
        {
            read = stream.Read(header, 0, header.Length);
        }

        if (!MatchesSignature(expectedType, header, read))
            return "The file is not a valid image.";

        contentType = expectedType;
        return null;
    }

    private static bool MatchesSignature(string type, byte[] h, int n) => type switch
    {
        "image/jpeg" => n >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
        "image/png" => n >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47
                              && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A,
        "image/gif" => n >= 6 && h[0] == 'G' && h[1] == 'I' && h[2] == 'F' && h[3] == '8',
        "image/webp" => n >= 12 && h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F'
                               && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P',
        _ => false
    };
}
