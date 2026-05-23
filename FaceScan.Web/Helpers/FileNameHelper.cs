using System.Text;
using System.Text.RegularExpressions;

namespace FaceScan.Web.Helpers;

public static class FileNameHelper
{
    private static readonly Regex InvalidCharsRegex = new("[^a-zA-Z0-9._-]", RegexOptions.Compiled);

    public static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        name = InvalidCharsRegex.Replace(name, "_");
        return string.IsNullOrWhiteSpace(name) ? $"file_{Guid.NewGuid():N}" : name;
    }

    public static string BuildSafePath(string rootPath, params string[] segments)
    {
        var combined = Path.Combine(new[] { rootPath }.Concat(segments).ToArray());
        var full = Path.GetFullPath(combined);
        var rootFull = Path.GetFullPath(rootPath);

        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid path traversal attempt.");
        }

        return full;
    }

    public static string CreateStoredFileName(string originalFileName)
    {
        var ext = Path.GetExtension(originalFileName);
        var baseName = Path.GetFileNameWithoutExtension(originalFileName);
        var sanitizedBase = SanitizeFileName(baseName);
        return $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}_{sanitizedBase}{ext}";
    }
}
