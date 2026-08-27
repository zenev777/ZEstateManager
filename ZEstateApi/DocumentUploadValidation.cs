// DocumentUploadValidation.cs

// Shared by every upload endpoint (repair invoices, meeting minutes, general
// building documents) so the "PDF/images only, 10 MB max" rule from the Trello
// card is enforced consistently in one place instead of drifting per controller.
public static class DocumentUploadValidation
{
    public const long MaxBytes = 10 * 1024 * 1024;

    private static readonly string[] AllowedExtensions = [".pdf", ".jpg", ".jpeg", ".png", ".webp"];
    private static readonly string[] AllowedContentTypes =
        ["application/pdf", "image/jpeg", "image/png", "image/webp"];

    public static string? Validate(IFormFile file)
    {
        if (file.Length == 0)
            return "Файлът е празен.";

        if (file.Length > MaxBytes)
            return "Файлът е твърде голям (макс. 10 MB).";

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension) || !AllowedContentTypes.Contains(file.ContentType))
            return "Позволени са само PDF и изображения (JPG, PNG, WEBP).";

        return null;
    }
}
