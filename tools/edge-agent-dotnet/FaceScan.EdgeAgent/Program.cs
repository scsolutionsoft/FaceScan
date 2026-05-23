using System.Globalization;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

AgentEnvLoader.Load(args);

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHttpClient();
builder.Services.AddHostedService<EdgeAgentWorker>();

var host = builder.Build();
await host.RunAsync();

internal sealed class EdgeAgentWorker : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EdgeAgentWorker> _logger;
    private readonly AgentOptions _options;

    public EdgeAgentWorker(IHttpClientFactory httpClientFactory, ILogger<EdgeAgentWorker> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = AgentOptions.FromEnvironment();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Edge agent started for station {StationCode}", _options.StationCode);

        var cameraClient = _httpClientFactory.CreateClient("camera");
        cameraClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        var serverClient = _httpClientFactory.CreateClient("server");
        serverClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        var frameCount = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            var startedAt = DateTime.UtcNow;
            frameCount++;

            try
            {
                var snapshotBytes = await FetchSnapshotAsync(cameraClient, stoppingToken);
                if (snapshotBytes is not null)
                {
                    if (frameCount % _options.HeartbeatEveryNFrames == 0)
                    {
                        await SendHeartbeatAsync(serverClient, "alive", stoppingToken);
                    }

                    var shouldVerify = true;
                    if (_options.EnablePreview)
                    {
                        var preview = await CallScanApiAsync(serverClient, "/Scan/Preview", snapshotBytes, stoppingToken);
                        shouldVerify = IsPreviewCandidate(preview, _options.MinConfidence);
                    }

                    if (shouldVerify)
                    {
                        var verify = await CallScanApiAsync(serverClient, "/Scan/Verify", snapshotBytes, stoppingToken);
                        LogVerifyResult(verify);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Edge agent loop failed");
            }

            var elapsed = DateTime.UtcNow - startedAt;
            var wait = TimeSpan.FromMilliseconds(Math.Max(_options.IntervalMs - elapsed.TotalMilliseconds, 0));
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, stoppingToken);
            }
        }
    }

    private async Task<byte[]?> FetchSnapshotAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _options.SnapshotUrl);
        if (!string.IsNullOrWhiteSpace(_options.CameraUsername))
        {
            var basic = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_options.CameraUsername}:{_options.CameraPassword}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Snapshot HTTP {StatusCode}", (int)response.StatusCode);
            return null;
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task<JsonElement?> CallScanApiAsync(HttpClient client, string endpoint, byte[] imageBytes, CancellationToken cancellationToken)
    {
        using var form = BuildScanForm(imageBytes);
        using var response = await client.PostAsync($"{_options.ServerBaseUrl}{endpoint}", form, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("{Endpoint} HTTP {StatusCode}", endpoint, (int)response.StatusCode);
            return null;
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.Clone();
    }

    private async Task SendHeartbeatAsync(HttpClient client, string message, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(_options.StationCode), "StationCode" },
            { new StringContent(_options.StationToken), "StationToken" },
            { new StringContent(_options.AgentId), "AgentId" },
            { new StringContent(message), "Message" },
            { new StringContent(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)), "CapturedAtUtc" }
        };

        using var response = await client.PostAsync($"{_options.ServerBaseUrl}/EdgeAgent/Heartbeat", form, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Heartbeat HTTP {StatusCode}", (int)response.StatusCode);
        }
    }

    private MultipartFormDataContent BuildScanForm(byte[] imageBytes)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(_options.StationCode), "StationCode" },
            { new StringContent(_options.StationToken), "StationToken" },
            { new StringContent("auto"), "RecognitionProfile" },
            { new StringContent(DateTime.Now.ToString("O", CultureInfo.InvariantCulture)), "ClientCapturedAtLocal" }
        };

        var image = new ByteArrayContent(imageBytes);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(image, "Image", "frame.jpg");
        return form;
    }

    private static bool IsPreviewCandidate(JsonElement? payload, decimal minConfidence)
    {
        if (!payload.HasValue)
        {
            return false;
        }

        var root = payload.Value;
        if (!root.TryGetProperty("success", out var success) || !success.GetBoolean())
        {
            return false;
        }

        if (!root.TryGetProperty("confidence", out var confidenceElement))
        {
            return false;
        }

        return confidenceElement.ValueKind switch
        {
            JsonValueKind.Number => confidenceElement.GetDecimal() >= minConfidence,
            _ => false
        };
    }

    private void LogVerifyResult(JsonElement? payload)
    {
        if (!payload.HasValue)
        {
            return;
        }

        var root = payload.Value;
        var success = root.TryGetProperty("success", out var successElement) && successElement.GetBoolean();
        var message = root.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : null;

        if (success)
        {
            var studentCode = root.TryGetProperty("studentCode", out var studentCodeElement) ? studentCodeElement.GetString() : "-";
            _logger.LogInformation("Verify success for student {StudentCode}", studentCode);
        }
        else
        {
            _logger.LogInformation("Verify no-match: {Message}", message ?? "-");
        }
    }
}

internal sealed record AgentOptions(
    string ServerBaseUrl,
    string StationCode,
    string StationToken,
    string SnapshotUrl,
    string AgentId,
    string? CameraUsername,
    string? CameraPassword,
    int IntervalMs,
    decimal MinConfidence,
    int HeartbeatEveryNFrames,
    bool EnablePreview,
    int TimeoutSeconds)
{
    public static AgentOptions FromEnvironment()
    {
        var server = Require("EDGE_SERVER_URL");
        var stationCode = Require("EDGE_STATION_CODE");
        var stationToken = Require("EDGE_STATION_TOKEN");
        var snapshotUrl = Require("EDGE_SNAPSHOT_URL");

        var agentId = Environment.GetEnvironmentVariable("EDGE_AGENT_ID");
        if (string.IsNullOrWhiteSpace(agentId))
        {
            agentId = $"{Environment.MachineName}-{stationCode}";
        }

        var intervalMs = ParseInt("EDGE_INTERVAL_MS", 1500, 500, 10000);
        var minConfidence = ParseDecimal("EDGE_MIN_CONFIDENCE", 0.60m, 0m, 1m);
        var heartbeatEvery = ParseInt("EDGE_HEARTBEAT_EVERY_N_FRAMES", 10, 1, 1000);
        var timeoutSec = ParseInt("EDGE_TIMEOUT_SECONDS", 8, 3, 60);
        var enablePreview = ParseBool("EDGE_ENABLE_PREVIEW", true);

        return new AgentOptions(
            ServerBaseUrl: server.TrimEnd('/'),
            StationCode: stationCode,
            StationToken: stationToken,
            SnapshotUrl: snapshotUrl,
            AgentId: agentId,
            CameraUsername: Environment.GetEnvironmentVariable("EDGE_CAMERA_USERNAME"),
            CameraPassword: Environment.GetEnvironmentVariable("EDGE_CAMERA_PASSWORD"),
            IntervalMs: intervalMs,
            MinConfidence: minConfidence,
            HeartbeatEveryNFrames: heartbeatEvery,
            EnablePreview: enablePreview,
            TimeoutSeconds: timeoutSec);
    }

    private static string Require(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required environment variable: {key}");
        }

        return value;
    }

    private static int ParseInt(string key, int fallback, int min, int max)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (!int.TryParse(raw, out var value))
        {
            value = fallback;
        }

        return Math.Clamp(value, min, max);
    }

    private static decimal ParseDecimal(string key, decimal fallback, decimal min, decimal max)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (!decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            value = fallback;
        }

        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static bool ParseBool(string key, bool fallback)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => fallback
        };
    }
}

internal static class AgentEnvLoader
{
    public static void Load(string[] args)
    {
        var candidatePaths = new List<string>();

        var explicitPath = FindExplicitEnvFileArg(args);
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            candidatePaths.Add(explicitPath);
        }

        var envPath = Environment.GetEnvironmentVariable("EDGE_ENV_FILE");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            candidatePaths.Add(envPath);
        }

        var baseDir = AppContext.BaseDirectory;
        candidatePaths.Add(Path.Combine(baseDir, "edge-agent.env"));

        var entryDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(entryDir))
        {
            candidatePaths.Add(Path.Combine(entryDir, "edge-agent.env"));
        }

        foreach (var path in candidatePaths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            LoadFile(path);
            break;
        }
    }

    private static string? FindExplicitEnvFileArg(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--env-file", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static void LoadFile(string path)
    {
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var idx = line.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (value.Length >= 2 &&
                ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\''))))
            {
                value = value[1..^1];
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
