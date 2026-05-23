using FaceScan.Web.Models.Options;
using Microsoft.AspNetCore.Http;

namespace FaceScan.Web.Validators;

public static class FileValidator
{
    private static readonly HashSet<string> ContentTypeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpg",
        "image/pjpeg"
    };

    public static bool IsValidImage(IFormFile file, UploadSettings settings)
    {
        if (file.Length <= 0)
        {
            return false;
        }

        var maxBytes = settings.MaxFileSizeMb * 1024L * 1024L;
        if (file.Length > maxBytes)
        {
            return false;
        }

        var contentType = NormalizeContentType(file.ContentType);
        var extension = Path.GetExtension(file.FileName);

        var allowedContentTypes = (settings.AllowedImageTypes ?? [])
            .Select(NormalizeContentType)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allowedExtensions = (settings.AllowedImageExtensions ?? [])
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.StartsWith('.') ? x.ToLowerInvariant() : $".{x.ToLowerInvariant()}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(contentType))
        {
            return IsAllowedByExtension(extension, allowedExtensions);
        }

        if (allowedContentTypes.Contains(contentType))
        {
            return true;
        }

        if (ContentTypeAliases.Contains(contentType) && allowedContentTypes.Contains("image/jpeg"))
        {
            return true;
        }

        if (contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return IsAllowedByExtension(extension, allowedExtensions);
        }

        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return IsAllowedByExtension(extension, allowedExtensions);
        }

        return false;
    }

    private static bool IsAllowedByExtension(string? extension, IReadOnlySet<string> allowedExtensions)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        if (allowedExtensions.Count == 0)
        {
            return true;
        }

        var normalized = extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
        return allowedExtensions.Contains(normalized);
    }

    private static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return string.Empty;
        }

        var trimmed = contentType.Trim();
        var separatorIndex = trimmed.IndexOf(';');
        if (separatorIndex >= 0)
        {
            trimmed = trimmed[..separatorIndex];
        }

        return trimmed.Trim().ToLowerInvariant();
    }
}
