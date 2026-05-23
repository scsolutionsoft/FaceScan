using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FaceScan.Web.Models.Options;
using FaceScan.Web.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace FaceScan.Web.Services;

public sealed class MultiEngineFacePythonClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly FaceRecognitionOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly IFaceRecognitionLoadGate _loadGate;
    private readonly ILogger<MultiEngineFacePythonClient> _logger;

    public MultiEngineFacePythonClient(
        IOptions<FaceRecognitionOptions> options,
        IWebHostEnvironment environment,
        IFaceRecognitionLoadGate loadGate,
        ILogger<MultiEngineFacePythonClient> logger)
    {
        _options = options.Value;
        _environment = environment;
        _loadGate = loadGate;
        _logger = logger;
    }

    internal Task<MultiEngineExtractResponse> ExtractAsync(
        IReadOnlyList<string> imagePaths,
        string recognitionProfile,
        IReadOnlyList<string> requestedEngines,
        CancellationToken cancellationToken = default)
    {
        var request = new MultiEngineExtractRequest
        {
            Mode = "multi_extract",
            RecognitionProfile = recognitionProfile,
            ImagePaths = imagePaths.Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            RequestedEngines = requestedEngines.Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            DetectionModel = NormalizeDetectionModel(_options.DlibDetectionModel),
            EncodingModel = NormalizeEncodingModel(_options.DlibEncodingModel),
            NumJitters = Math.Clamp(_options.DlibNumJitters, 1, 50),
            UpsampleTimes = Math.Clamp(_options.DlibUpsampleTimes, 0, 4),
            MinFaceQualityScore = Math.Clamp(_options.DlibMinFaceQualityScore, 0.05m, 0.85m)
        };

        return InvokePythonAsync(request, cancellationToken);
    }

    internal async Task<MultiEngineExtractResponse> ExtractProbeAsync(
        byte[] imageBytes,
        string recognitionProfile,
        IReadOnlyList<string> requestedEngines,
        CancellationToken cancellationToken = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"facescan-multi-probe-{Guid.NewGuid():N}.jpg");
        await File.WriteAllBytesAsync(tempPath, imageBytes, cancellationToken);

        try
        {
            return await ExtractAsync([tempPath], recognitionProfile, requestedEngines, cancellationToken);
        }
        finally
        {
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
                // Ignore temp cleanup errors.
            }
        }
    }

    private async Task<MultiEngineExtractResponse> InvokePythonAsync(
        MultiEngineExtractRequest request,
        CancellationToken cancellationToken)
    {
        using var lease = await _loadGate.EnterAsync();

        var scriptPath = ResolveScriptPath();
        if (!File.Exists(scriptPath))
        {
            return new MultiEngineExtractResponse
            {
                Ok = false,
                Error = $"Face engine script was not found at: {scriptPath}"
            };
        }

        var requestJson = JsonSerializer.Serialize(request);
        var executables = BuildPythonExecutableCandidates();

        MultiEngineExtractResponse? lastFailure = null;
        foreach (var executable in executables)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await InvokePythonWithExecutableAsync(executable, scriptPath, requestJson);
            if (response.Ok)
            {
                return response;
            }

            lastFailure = response;
            if (!IsExecutableNotFound(response))
            {
                return response;
            }
        }

        return lastFailure ?? new MultiEngineExtractResponse
        {
            Ok = false,
            Error = "ไม่สามารถเริ่มต้นโปรเซส Python ได้"
        };
    }

    private async Task<MultiEngineExtractResponse> InvokePythonWithExecutableAsync(
        string pythonExecutable,
        string scriptPath,
        string requestJson)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = $"\"{scriptPath}\"",
                WorkingDirectory = _environment.ContentRootPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true
            }
        };

        try
        {
            if (!process.Start())
            {
                return new MultiEngineExtractResponse
                {
                    Ok = false,
                    Error = $"ไม่สามารถเริ่มต้นโปรเซส Python ด้วย executable '{pythonExecutable}' ได้"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ไม่สามารถเริ่มต้นโปรเซส Python ด้วย executable '{Executable}' ได้", pythonExecutable);
            return new MultiEngineExtractResponse
            {
                Ok = false,
                Error = $"ไม่สามารถเริ่มต้นโปรเซส Python ด้วย executable '{pythonExecutable}' ได้: {ex.Message}"
            };
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.StandardInput.WriteAsync(requestJson);
        await process.StandardInput.FlushAsync();
        process.StandardInput.Close();

        var timeout = TimeSpan.FromSeconds(Math.Clamp(_options.DlibProcessTimeoutSeconds, 5, 180));
        var waitTask = process.WaitForExitAsync();
        var completedTask = await Task.WhenAny(waitTask, Task.Delay(timeout));

        if (completedTask != waitTask)
        {
            TryKillProcess(process);

            var timeoutStdout = await stdoutTask;
            var timeoutStderr = await stderrTask;

            return new MultiEngineExtractResponse
            {
                Ok = false,
                Error = $"Python process timed out after {timeout.TotalSeconds:0} seconds.",
                RawStdout = TrimForLog(timeoutStdout),
                RawStderr = TrimForLog(timeoutStderr)
            };
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            _logger.LogWarning(
                "Face engine process exited with code {ExitCode}. executable: {Executable}, stderr: {Stderr}",
                process.ExitCode,
                pythonExecutable,
                TrimForLog(stderr));

            return new MultiEngineExtractResponse
            {
                Ok = false,
                Error = $"Python process exited with code {process.ExitCode}.",
                RawStdout = TrimForLog(stdout),
                RawStderr = TrimForLog(stderr)
            };
        }

        if (string.IsNullOrWhiteSpace(stdout))
        {
            return new MultiEngineExtractResponse
            {
                Ok = false,
                Error = "Python process returned empty output.",
                RawStderr = TrimForLog(stderr)
            };
        }

        try
        {
            var response = JsonSerializer.Deserialize<MultiEngineExtractResponse>(stdout, JsonOptions);
            if (response is null)
            {
                return new MultiEngineExtractResponse
                {
                    Ok = false,
                    Error = "รูปแบบผลลัพธ์จากสคริปต์ face engine ไม่ถูกต้อง",
                    RawStdout = TrimForLog(stdout),
                    RawStderr = TrimForLog(stderr)
                };
            }

            response.Results ??= [];
            response.AvailableEngines ??= [];
            response.RawStdout ??= TrimForLog(stdout);
            response.RawStderr ??= string.IsNullOrWhiteSpace(stderr) ? null : TrimForLog(stderr);

            foreach (var item in response.Results)
            {
                item.Engines ??= new Dictionary<string, MultiEngineFaceEngineResult>(StringComparer.OrdinalIgnoreCase);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ไม่สามารถแปลงผลลัพธ์จากสคริปต์ face engine ได้");
            return new MultiEngineExtractResponse
            {
                Ok = false,
                Error = $"ไม่สามารถแปลงผลลัพธ์ face engine ได้: {ex.Message}",
                RawStdout = TrimForLog(stdout),
                RawStderr = TrimForLog(stderr)
            };
        }
    }

    private IEnumerable<string> BuildPythonExecutableCandidates()
    {
        var candidates = new List<string>();

        void Add(string? executable)
        {
            if (string.IsNullOrWhiteSpace(executable))
            {
                return;
            }

            var normalized = executable.Trim();
            if (!candidates.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(normalized);
            }
        }

        Add(_options.DlibPythonExecutable);
        Add("python");
        Add("py");
        Add("python3");

        return candidates;
    }

    private string ResolveScriptPath()
    {
        if (Path.IsPathRooted(_options.DlibScriptPath))
        {
            return _options.DlibScriptPath;
        }

        return Path.Combine(_environment.ContentRootPath, _options.DlibScriptPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static bool IsExecutableNotFound(MultiEngineExtractResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.Error))
        {
            return false;
        }

        var error = response.Error;
        return error.Contains("code 9009", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("ไม่พบไฟล์ที่ต้องการ", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("ไม่พบไฟล์หรือโฟลเดอร์", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("ไม่รู้จักคำสั่งนี้", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Ignore cleanup errors.
        }
    }

    private static string NormalizeDetectionModel(string? value)
    {
        return string.Equals(value, "cnn", StringComparison.OrdinalIgnoreCase)
            ? "cnn"
            : "hog";
    }

    private static string NormalizeEncodingModel(string? value)
    {
        return string.Equals(value, "large", StringComparison.OrdinalIgnoreCase)
            ? "large"
            : "small";
    }

    private static string? TrimForLog(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        return trimmed.Length <= 2000
            ? trimmed
            : trimmed[..2000];
    }
}

internal sealed class MultiEngineExtractRequest
{
    public string Mode { get; set; } = "multi_extract";
    public string RecognitionProfile { get; set; } = string.Empty;
    public List<string> ImagePaths { get; set; } = [];
    public List<string> RequestedEngines { get; set; } = [];
    public string DetectionModel { get; set; } = "hog";
    public string EncodingModel { get; set; } = "small";
    public int NumJitters { get; set; } = 1;
    public int UpsampleTimes { get; set; } = 1;
    public decimal MinFaceQualityScore { get; set; } = 0.32m;
}

internal sealed class MultiEngineExtractResponse
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public List<string>? AvailableEngines { get; set; } = [];
    public List<MultiEngineFaceResult>? Results { get; set; } = [];
    public string? RawStdout { get; set; }
    public string? RawStderr { get; set; }
}

internal sealed class MultiEngineFaceResult
{
    public string ImagePath { get; set; } = string.Empty;
    public int FaceCount { get; set; }
    public double QualityScore { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, MultiEngineFaceEngineResult>? Engines { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class MultiEngineFaceEngineResult
{
    public string Engine { get; set; } = string.Empty;
    public string Kind { get; set; } = "vector";
    public string Metric { get; set; } = "euclidean";
    public int FaceCount { get; set; }
    public double QualityScore { get; set; }
    public List<double>? Vector { get; set; }
    public string? HashHex { get; set; }
    public double? Brightness { get; set; }
    public double? Contrast { get; set; }
    public string? Error { get; set; }
}
