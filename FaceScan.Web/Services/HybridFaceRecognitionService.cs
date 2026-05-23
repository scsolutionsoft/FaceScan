using System.Globalization;
using System.Numerics;
using System.Text.Json;
using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.Models;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Models.Enums;
using FaceScan.Web.Models.Options;
using FaceScan.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FaceScan.Web.Services;

public sealed partial class HybridFaceRecognitionService : IFaceRecognitionService
{
    private const string ProviderName = "HybridFaceEngine";
    private const string EmbeddingVersion = "hybrid-face-v1";
    private const string StudentTemplateCacheKey = "hybrid-face-templates:students";
    private const string TeacherTemplateCacheKey = "hybrid-face-templates:teachers";

    private const string MockEngine = "mock_phash";
    private const string OpenCvLiteEngine = "opencv_lite";
    private const string DlibEngine = "dlib";
    private const string InsightFaceEngine = "insightface";
    private const string DeepFaceEngine = "deepface";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly MemoryCacheEntryOptions TemplateCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
        SlidingExpiration = TimeSpan.FromMinutes(3)
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly MultiEngineFacePythonClient _pythonClient;
    private readonly MockFaceRecognitionService _fallbackService;
    private readonly FaceRecognitionOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<HybridFaceRecognitionService> _logger;

    public HybridFaceRecognitionService(
        ApplicationDbContext dbContext,
        MultiEngineFacePythonClient pythonClient,
        MockFaceRecognitionService fallbackService,
        IOptions<FaceRecognitionOptions> options,
        IWebHostEnvironment environment,
        IMemoryCache memoryCache,
        ILogger<HybridFaceRecognitionService> logger)
    {
        _dbContext = dbContext;
        _pythonClient = pythonClient;
        _fallbackService = fallbackService;
        _options = options.Value;
        _environment = environment;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<bool> RemoveStudentProfileAsync(int studentId)
    {
        var profile = await _dbContext.FaceProfiles.FirstOrDefaultAsync(x => x.StudentId == studentId);
        if (profile is null)
        {
            return false;
        }

        _dbContext.FaceProfiles.Remove(profile);
        await _dbContext.SaveChangesAsync();
        InvalidateStudentTemplateCache();
        return true;
    }

    public async Task<bool> RemoveTeacherProfileAsync(string userId)
    {
        var profile = await _dbContext.TeacherFaceProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
        if (profile is null)
        {
            return false;
        }

        _dbContext.TeacherFaceProfiles.Remove(profile);
        await _dbContext.SaveChangesAsync();
        InvalidateTeacherTemplateCache();
        return true;
    }

    private static string NormalizeRecognitionProfile(string? recognitionProfile)
    {
        return recognitionProfile?.Trim().ToLowerInvariant() switch
        {
            FaceRecognitionProfiles.Fast or "speed" or "quick" or "mock" => FaceRecognitionProfiles.Fast,
            FaceRecognitionProfiles.Accurate or "accuracy" or "precision" or "dlib" => FaceRecognitionProfiles.Accurate,
            FaceRecognitionProfiles.Stable or "steady" or "reliable" => FaceRecognitionProfiles.Stable,
            FaceRecognitionProfiles.Balanced or FaceRecognitionProfiles.Auto or "hybrid" or "smart" or "default" => FaceRecognitionProfiles.Auto,
            _ => FaceRecognitionProfiles.Auto
        };
    }

    private static string BuildProviderLabel(IEnumerable<string> engines)
    {
        var items = engines
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(GetEngineSortOrder)
            .ToList();

        if (items.Count == 0)
        {
            return ProviderName;
        }

        return $"{ProviderName}[{string.Join("+", items)}]";
    }

    private double GetEngineWeight(string engine)
    {
        return engine.ToLowerInvariant() switch
        {
            InsightFaceEngine => 1.20d,
            DlibEngine => 1.10d,
            DeepFaceEngine => 1.00d,
            OpenCvLiteEngine => 0.95d,
            MockEngine => 0.65d,
            _ => 0.80d
        };
    }

    private string ResolveEngineMetric(string engine)
    {
        return engine.ToLowerInvariant() switch
        {
            DlibEngine => "euclidean",
            InsightFaceEngine => "cosine",
            DeepFaceEngine => "cosine",
            _ => "mock_similarity"
        };
    }

    private double ResolveEngineThreshold(string engine)
    {
        return engine.ToLowerInvariant() switch
        {
            DlibEngine => Math.Clamp((double)_options.DlibDistanceThreshold, 0.25d, 1.0d),
            InsightFaceEngine => Math.Clamp((double)_options.InsightFaceSimilarityThreshold, 0.10d, 0.95d),
            DeepFaceEngine => Math.Clamp((double)_options.DeepFaceSimilarityThreshold, 0.10d, 0.95d),
            OpenCvLiteEngine => Math.Clamp((double)_options.OpenCvLiteSimilarityThreshold, 0.35d, 0.98d),
            _ => Math.Clamp((double)_options.MockSimilarityThreshold, 0.35d, 0.95d)
        };
    }

    private void InvalidateStudentTemplateCache()
    {
        _memoryCache.Remove(StudentTemplateCacheKey);
    }

    private void InvalidateTeacherTemplateCache()
    {
        _memoryCache.Remove(TeacherTemplateCacheKey);
    }

    private static int GetEngineSortOrder(string engine)
    {
        return engine.ToLowerInvariant() switch
        {
            OpenCvLiteEngine => 0,
            DlibEngine => 1,
            InsightFaceEngine => 2,
            DeepFaceEngine => 3,
            MockEngine => 4,
            _ => 9
        };
    }
}
