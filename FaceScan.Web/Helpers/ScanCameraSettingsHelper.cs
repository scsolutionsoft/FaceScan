using System.Text.Json;

namespace FaceScan.Web.Helpers;

public sealed class ScanCameraSettings
{
    public string FaceMode { get; set; } = "single"; // single | multi
    public string ScanEngine { get; set; } = "classic"; // classic | buffered
    public bool AutoTuneEnabled { get; set; } = true;
    public int MultiFaceMax { get; set; } = 3;
}

public static class ScanCameraSettingsHelper
{
    private const string Prefix = "[SCANCFG]";
    private const string Suffix = "[/SCANCFG]";

    public static ScanCameraSettings Parse(string? notes)
    {
        var defaults = new ScanCameraSettings();
        if (string.IsNullOrWhiteSpace(notes))
        {
            return defaults;
        }

        var start = notes.IndexOf(Prefix, StringComparison.Ordinal);
        var end = notes.IndexOf(Suffix, StringComparison.Ordinal);
        if (start < 0 || end <= start)
        {
            return defaults;
        }

        var jsonStart = start + Prefix.Length;
        var json = notes.Substring(jsonStart, end - jsonStart);
        if (string.IsNullOrWhiteSpace(json))
        {
            return defaults;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<ScanCameraSettings>(json);
            if (parsed is null)
            {
                return defaults;
            }

            parsed.FaceMode = NormalizeFaceMode(parsed.FaceMode);
            parsed.ScanEngine = NormalizeScanEngine(parsed.ScanEngine);
            parsed.MultiFaceMax = Math.Clamp(parsed.MultiFaceMax, 2, 6);
            return parsed;
        }
        catch
        {
            return defaults;
        }
    }

    public static string StripMeta(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return string.Empty;
        }

        var start = notes.IndexOf(Prefix, StringComparison.Ordinal);
        var end = notes.IndexOf(Suffix, StringComparison.Ordinal);
        if (start < 0 || end <= start)
        {
            return notes.Trim();
        }

        var withoutMeta = notes.Remove(start, (end + Suffix.Length) - start);
        return withoutMeta.Trim();
    }

    public static string Merge(string? plainNotes, ScanCameraSettings settings)
    {
        var cleanNotes = StripMeta(plainNotes);
        var normalized = new ScanCameraSettings
        {
            FaceMode = NormalizeFaceMode(settings.FaceMode),
            ScanEngine = NormalizeScanEngine(settings.ScanEngine),
            AutoTuneEnabled = settings.AutoTuneEnabled,
            MultiFaceMax = Math.Clamp(settings.MultiFaceMax, 2, 6)
        };

        var json = JsonSerializer.Serialize(normalized);
        if (string.IsNullOrWhiteSpace(cleanNotes))
        {
            return $"{Prefix}{json}{Suffix}";
        }

        return $"{cleanNotes}\n{Prefix}{json}{Suffix}";
    }

    public static string NormalizeFaceMode(string? faceMode)
    {
        return string.Equals(faceMode, "multi", StringComparison.OrdinalIgnoreCase)
            ? "multi"
            : "single";
    }

    public static string NormalizeScanEngine(string? scanEngine)
    {
        return string.Equals(scanEngine, "buffered", StringComparison.OrdinalIgnoreCase)
            ? "buffered"
            : "classic";
    }
}
