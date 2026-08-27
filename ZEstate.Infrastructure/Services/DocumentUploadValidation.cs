// DocumentUploadValidation.cs
namespace ZEstate.Infrastructure.Services;

// Shared by every upload endpoint (repair invoices, meeting minutes, general
// building documents) so the "PDF/images only, 10 MB max" rule from the Trello
// card is enforced consistently in one place instead of drifting per controller.
// Works off primitives (not IFormFile) so it stays usable from services without
// pulling an ASP.NET Core Http dependency into the service layer.
public static class DocumentUploadValidation
{
    public const long MaxBytes = 10 * 1024 * 1024;

    private static readonly string[] AllowedExtensions = [".pdf", ".jpg", ".jpeg", ".png", ".webp"];
    private static readonly string[] AllowedContentTypes =
        ["application/pdf", "image/jpeg", "image/png", "image/webp"];

    public static string? Validate(long length, string fileName, string? contentType)
    {
        if (length == 0)
            return "Файлът е празен.";

        if (length > MaxBytes)
            return "Файлът е твърде голям (макс. 10 MB).";

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension) || contentType == null || !AllowedContentTypes.Contains(contentType))
            return "Позволени са само PDF и изображения (JPG, PNG, WEBP).";

        return null;
    }
}
