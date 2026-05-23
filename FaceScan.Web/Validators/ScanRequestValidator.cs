using FaceScan.Web.ViewModels.Scan;

namespace FaceScan.Web.Validators;

public static class ScanRequestValidator
{
    private const long MaxUploadBytes = 8 * 1024 * 1024;
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    public static string? Validate(ScanVerifyRequestViewModel? request)
    {
        if (request is null)
        {
            return "Invalid request payload.";
        }

        if (request.Image is null || request.Image.Length == 0)
        {
            return "Image is required.";
        }

        if (request.Image.Length > MaxUploadBytes)
        {
            return "Image file size exceeds the allowed limit.";
        }

        if (!IsValidImageType(request.Image.ContentType, request.Image.FileName))
        {
            return "Invalid image file type.";
        }

        if (request.Latitude.HasValue && (request.Latitude.Value < -90m || request.Latitude.Value > 90m))
        {
            return "Invalid latitude value.";
        }

        if (request.Longitude.HasValue && (request.Longitude.Value < -180m || request.Longitude.Value > 180m))
        {
            return "Invalid longitude value.";
        }

        return null;
    }

    private static bool IsValidImageType(string? contentType, string? fileName)
    {
        var normalized = NormalizeContentType(contentType);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return HasAllowedImageExtension(fileName);
        }

        if (normalized.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return HasAllowedImageExtension(fileName);
        }

        return false;
    }

    private static bool HasAllowedImageExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);
        return !string.IsNullOrWhiteSpace(extension) && AllowedImageExtensions.Contains(extension);
    }

    private static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return string.Empty;
        }

        var value = contentType.Trim();
        var separatorIndex = value.IndexOf(';');
        if (separatorIndex >= 0)
        {
            value = value[..separatorIndex];
        }

        return value.Trim().ToLowerInvariant();
    }
}
