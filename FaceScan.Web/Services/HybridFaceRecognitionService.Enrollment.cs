using System.Globalization;
using System.Text.Json;
using FaceScan.Web.Helpers;
using FaceScan.Web.Models;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace FaceScan.Web.Services;

public sealed partial class HybridFaceRecognitionService
{
    public async Task<FaceEnrollResult> EnrollStudentAsync(int studentId, List<string> imagePaths)
    {
        var student = await _dbContext.Students
            .Include(x => x.FaceProfile)
            .FirstOrDefaultAsync(x => x.Id == studentId);

        if (student is null)
        {
            return new FaceEnrollResult
            {
                Success = false,
                Message = "ไม่พบข้อมูลนักเรียน",
                Provider = ProviderName,
                QualityScore = 0m
            };
        }

        var fullPathMap = BuildPhysicalPathMap(imagePaths);
        var maxEnrollmentImages = Math.Clamp(_options.DlibMaxEnrollmentImages, 1, 30);
        var selectedPaths = fullPathMap.Keys.Take(maxEnrollmentImages).ToList();

        if (selectedPaths.Count == 0)
        {
            return new FaceEnrollResult
            {
                Success = false,
                Message = "ไม่พบรูปลงทะเบียนที่ใช้งานได้",
                Provider = ProviderName,
                QualityScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new { error = "no_valid_image_paths" })
            };
        }

        var extraction = await _pythonClient.ExtractAsync(
            selectedPaths,
            FaceRecognitionProfiles.Accurate,
            BuildEnrollmentEnginePlan().RequestedEngines);

        if (!extraction.Ok)
        {
            return await TryFallbackEnrollAsync(
                studentId,
                imagePaths,
                "ไม่สามารถประมวลผลรูปสำหรับลงทะเบียนได้",
                extraction);
        }

        var validResults = extraction.Results!
            .Where(HasUsableEngineFeature)
            .ToList();

        var minImages = Math.Clamp(_options.DlibMinEnrollmentImages, 1, 10);
        var profile = student.FaceProfile;
        if (profile is null)
        {
            profile = new FaceProfile
            {
                StudentId = student.Id,
                TemplateVersion = EmbeddingVersion
            };

            _dbContext.FaceProfiles.Add(profile);
        }

        if (validResults.Count < minImages)
        {
            profile.EnrollmentStatus = EnrollmentStatus.Pending;
            profile.TemplateVersion = EmbeddingVersion;
            profile.LastTrainedAt = DateTime.UtcNow;
            profile.EmbeddingJson = null;
            profile.QualityNote = $"ต้องมีรูปที่ใช้งานได้อย่างน้อย {minImages} รูป (ปัจจุบัน: {validResults.Count} รูป)";
            await _dbContext.SaveChangesAsync();
            InvalidateStudentTemplateCache();

            var pendingQuality = validResults.Count == 0
                ? 0d
                : validResults.Average(x => x.QualityScore);

            return new FaceEnrollResult
            {
                Success = false,
                Message = $"ต้องมีรูปที่ใช้งานได้อย่างน้อย {minImages} รูป",
                Provider = ProviderName,
                QualityScore = Math.Round((decimal)pendingQuality, 4),
                RawResponseJson = JsonSerializer.Serialize(new
                {
                    status = "pending",
                    validImageCount = validResults.Count,
                    requestedImageCount = selectedPaths.Count,
                    minImageCount = minImages,
                    availableEngines = extraction.AvailableEngines
                })
            };
        }

        var templates = BuildHybridTemplates(validResults);
        if (templates.Count == 0)
        {
            return await TryFallbackEnrollAsync(
                studentId,
                imagePaths,
                "ไม่พบคุณลักษณะใบหน้าที่พร้อมใช้งานสำหรับลงทะเบียน",
                extraction);
        }

        var averageQuality = validResults.Average(x => x.QualityScore);
        var engineNames = templates.Select(x => x.Engine).ToList();

        profile.EnrollmentStatus = EnrollmentStatus.Ready;
        profile.TemplateVersion = EmbeddingVersion;
        profile.LastTrainedAt = DateTime.UtcNow;
        profile.EmbeddingJson = JsonSerializer.Serialize(new HybridEmbeddingPayload
        {
            Version = EmbeddingVersion,
            TrainedAtUtc = DateTime.UtcNow,
            Engines = templates
        });
        profile.QualityNote = $"ลงทะเบียนรูปแล้ว {validResults.Count} รูป, คุณภาพเฉลี่ย {(averageQuality * 100):0.#}% | เอนจิน: {string.Join(", ", engineNames)}";

        await _dbContext.SaveChangesAsync();
        InvalidateStudentTemplateCache();

        return new FaceEnrollResult
        {
            Success = true,
            Message = "ลงทะเบียนใบหน้าเรียบร้อย",
            Provider = BuildProviderLabel(engineNames),
            QualityScore = Math.Round((decimal)averageQuality, 4),
            RawResponseJson = JsonSerializer.Serialize(new
            {
                status = "ready",
                validImageCount = validResults.Count,
                requestedImageCount = selectedPaths.Count,
                templateVersion = EmbeddingVersion,
                availableEngines = extraction.AvailableEngines,
                engines = engineNames,
                averageQuality = Math.Round(averageQuality, 4)
            })
        };
    }

    public async Task<FaceEnrollResult> EnrollTeacherAsync(string userId, List<string> imagePaths)
    {
        var user = await _dbContext.Users
            .Include(x => x.TeacherFaceProfile)
            .FirstOrDefaultAsync(x => x.Id == userId && x.IsActive);

        if (user is null)
        {
            return new FaceEnrollResult
            {
                Success = false,
                Message = "ไม่พบข้อมูลครู",
                Provider = ProviderName,
                QualityScore = 0m
            };
        }

        var fullPathMap = BuildPhysicalPathMap(imagePaths);
        var maxEnrollmentImages = Math.Clamp(_options.DlibMaxEnrollmentImages, 1, 30);
        var selectedPaths = fullPathMap.Keys.Take(maxEnrollmentImages).ToList();

        if (selectedPaths.Count == 0)
        {
            return new FaceEnrollResult
            {
                Success = false,
                Message = "ไม่พบรูปลงทะเบียนที่ใช้งานได้",
                Provider = ProviderName,
                QualityScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new { error = "no_valid_image_paths" })
            };
        }

        var extraction = await _pythonClient.ExtractAsync(
            selectedPaths,
            FaceRecognitionProfiles.Accurate,
            BuildEnrollmentEnginePlan().RequestedEngines);

        if (!extraction.Ok)
        {
            return await TryFallbackTeacherEnrollAsync(
                userId,
                imagePaths,
                "ไม่สามารถประมวลผลรูปสำหรับลงทะเบียนครูได้",
                extraction);
        }

        var validResults = extraction.Results!
            .Where(HasUsableEngineFeature)
            .ToList();

        var minImages = Math.Clamp(_options.DlibMinEnrollmentImages, 1, 10);
        var profile = user.TeacherFaceProfile;
        if (profile is null)
        {
            profile = new TeacherFaceProfile
            {
                UserId = user.Id,
                TemplateVersion = EmbeddingVersion
            };

            _dbContext.TeacherFaceProfiles.Add(profile);
        }

        if (validResults.Count < minImages)
        {
            profile.EnrollmentStatus = EnrollmentStatus.Pending;
            profile.TemplateVersion = EmbeddingVersion;
            profile.LastTrainedAt = DateTime.UtcNow;
            profile.EmbeddingJson = null;
            profile.QualityNote = $"ต้องมีรูปที่ใช้งานได้อย่างน้อย {minImages} รูป (ปัจจุบัน: {validResults.Count} รูป)";
            await _dbContext.SaveChangesAsync();
            InvalidateTeacherTemplateCache();

            var pendingQuality = validResults.Count == 0
                ? 0d
                : validResults.Average(x => x.QualityScore);

            return new FaceEnrollResult
            {
                Success = false,
                Message = $"ต้องมีรูปที่ใช้งานได้อย่างน้อย {minImages} รูป",
                Provider = ProviderName,
                QualityScore = Math.Round((decimal)pendingQuality, 4),
                RawResponseJson = JsonSerializer.Serialize(new
                {
                    status = "pending",
                    validImageCount = validResults.Count,
                    requestedImageCount = selectedPaths.Count,
                    minImageCount = minImages,
                    availableEngines = extraction.AvailableEngines
                })
            };
        }

        var templates = BuildHybridTemplates(validResults);
        if (templates.Count == 0)
        {
            return await TryFallbackTeacherEnrollAsync(
                userId,
                imagePaths,
                "ไม่พบคุณลักษณะใบหน้าที่พร้อมใช้งานสำหรับลงทะเบียนครู",
                extraction);
        }

        var averageQuality = validResults.Average(x => x.QualityScore);
        var engineNames = templates.Select(x => x.Engine).ToList();

        profile.EnrollmentStatus = EnrollmentStatus.Ready;
        profile.TemplateVersion = EmbeddingVersion;
        profile.LastTrainedAt = DateTime.UtcNow;
        profile.EmbeddingJson = JsonSerializer.Serialize(new HybridEmbeddingPayload
        {
            Version = EmbeddingVersion,
            TrainedAtUtc = DateTime.UtcNow,
            Engines = templates
        });
        profile.QualityNote = $"ลงทะเบียนรูปแล้ว {validResults.Count} รูป, คุณภาพเฉลี่ย {(averageQuality * 100):0.#}% | เอนจิน: {string.Join(", ", engineNames)}";

        await _dbContext.SaveChangesAsync();
        InvalidateTeacherTemplateCache();

        return new FaceEnrollResult
        {
            Success = true,
            Message = "ลงทะเบียนใบหน้าครูเรียบร้อย",
            Provider = BuildProviderLabel(engineNames),
            QualityScore = Math.Round((decimal)averageQuality, 4),
            RawResponseJson = JsonSerializer.Serialize(new
            {
                status = "ready",
                validImageCount = validResults.Count,
                requestedImageCount = selectedPaths.Count,
                templateVersion = EmbeddingVersion,
                availableEngines = extraction.AvailableEngines,
                engines = engineNames,
                averageQuality = Math.Round(averageQuality, 4)
            })
        };
    }

    private async Task<FaceEnrollResult> TryFallbackEnrollAsync(
        int studentId,
        List<string> imagePaths,
        string message,
        MultiEngineExtractResponse extraction)
    {
        _logger.LogWarning(
            "Hybrid student enrollment extraction failed. studentId: {StudentId}, error: {Error}",
            studentId,
            extraction.Error ?? "unknown_error");

        if (!_options.DlibAutoFallbackToMock)
        {
            return new FaceEnrollResult
            {
                Success = false,
                Message = message,
                Provider = ProviderName,
                QualityScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new
                {
                    error = "hybrid_extract_failed",
                    detail = extraction.Error ?? "unknown_error"
                })
            };
        }

        var fallbackResult = await _fallbackService.EnrollStudentAsync(studentId, imagePaths);
        return WrapFallbackEnrollResult(fallbackResult, extraction.Error);
    }

    private async Task<FaceEnrollResult> TryFallbackTeacherEnrollAsync(
        string userId,
        List<string> imagePaths,
        string message,
        MultiEngineExtractResponse extraction)
    {
        _logger.LogWarning(
            "Hybrid teacher enrollment extraction failed. userId: {UserId}, error: {Error}",
            userId,
            extraction.Error ?? "unknown_error");

        if (!_options.DlibAutoFallbackToMock)
        {
            return new FaceEnrollResult
            {
                Success = false,
                Message = message,
                Provider = ProviderName,
                QualityScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new
                {
                    error = "hybrid_extract_failed",
                    detail = extraction.Error ?? "unknown_error"
                })
            };
        }

        var fallbackResult = await _fallbackService.EnrollTeacherAsync(userId, imagePaths);
        return WrapFallbackEnrollResult(fallbackResult, extraction.Error);
    }

    private FaceEnrollResult WrapFallbackEnrollResult(FaceEnrollResult fallbackResult, string? primaryError)
    {
        var fallbackMessage = $"{fallbackResult.Message} (สลับไปใช้โหมดสำรองอัตโนมัติจาก Hybrid)";
        return new FaceEnrollResult
        {
            Success = fallbackResult.Success,
            Message = fallbackMessage,
            Provider = fallbackResult.Provider,
            QualityScore = fallbackResult.QualityScore,
            RawResponseJson = JsonSerializer.Serialize(new
            {
                fallback = true,
                primaryProvider = ProviderName,
                primaryError = primaryError ?? "unknown_error",
                fallbackProvider = fallbackResult.Provider,
                fallbackSuccess = fallbackResult.Success,
                fallbackRaw = fallbackResult.RawResponseJson
            })
        };
    }

    private List<HybridEngineTemplatePayload> BuildHybridTemplates(IReadOnlyList<MultiEngineFaceResult> results)
    {
        var buckets = new Dictionary<string, List<MultiEngineFaceEngineResult>>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in results)
        {
            if (result.Engines is null)
            {
                continue;
            }

            foreach (var entry in result.Engines)
            {
                if (!IsUsableEngineFeature(entry.Value))
                {
                    continue;
                }

                if (!buckets.TryGetValue(entry.Key, out var items))
                {
                    items = [];
                    buckets[entry.Key] = items;
                }

                items.Add(entry.Value);
            }
        }

        var templates = new List<HybridEngineTemplatePayload>(buckets.Count);
        foreach (var bucket in buckets.OrderBy(x => GetEngineSortOrder(x.Key)))
        {
            var engine = bucket.Key;
            var values = bucket.Value;
            if (values.Count == 0)
            {
                continue;
            }

            if (string.Equals(values[0].Kind, "vector", StringComparison.OrdinalIgnoreCase))
            {
                var vectors = values
                    .Where(x => IsValidVector(x.Vector))
                    .Select(x => x.Vector!.ToArray())
                    .Where(IsValidVector)
                    .ToList();

                if (vectors.Count == 0)
                {
                    continue;
                }

                templates.Add(new HybridEngineTemplatePayload
                {
                    Engine = engine,
                    Kind = "vector",
                    Metric = ResolveEngineMetric(engine),
                    Threshold = ResolveEngineThreshold(engine),
                    QualityScore = values.Average(x => x.QualityScore),
                    SampleCount = vectors.Count,
                    TemplateVector = BuildAverageVector(vectors).ToList()
                });

                continue;
            }

            var samples = values
                .Select(x => TryBuildHashSample(x.HashHex, x.Brightness, x.Contrast, x.QualityScore))
                .Where(static x => x is not null)
                .Cast<HashSample>()
                .ToList();

            if (samples.Count == 0)
            {
                continue;
            }

            templates.Add(new HybridEngineTemplatePayload
            {
                Engine = engine,
                Kind = "hash",
                Metric = ResolveEngineMetric(engine),
                Threshold = ResolveEngineThreshold(engine),
                QualityScore = samples.Average(x => x.QualityScore),
                SampleCount = samples.Count,
                HashSamples = samples.Select(x => new HybridHashSamplePayload
                {
                    HashHex = x.Hash.ToString("X16", CultureInfo.InvariantCulture),
                    Brightness = x.Brightness,
                    Contrast = x.Contrast,
                    QualityScore = x.QualityScore
                }).ToList()
            });
        }

        return templates;
    }

    private static double[] BuildAverageVector(IReadOnlyList<double[]> vectors)
    {
        var size = vectors[0].Length;
        var template = new double[size];

        foreach (var vector in vectors)
        {
            if (vector.Length != size)
            {
                continue;
            }

            for (var index = 0; index < size; index++)
            {
                template[index] += vector[index];
            }
        }

        for (var index = 0; index < size; index++)
        {
            template[index] /= vectors.Count;
        }

        return template;
    }

    private FaceEnginePlan BuildEnrollmentEnginePlan()
    {
        return new FaceEnginePlan(
            [OpenCvLiteEngine, DlibEngine, InsightFaceEngine, DeepFaceEngine, MockEngine],
            4,
            false);
    }

    private Dictionary<string, string> BuildPhysicalPathMap(IEnumerable<string> imagePaths)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var inputPath in imagePaths.Where(static x => !string.IsNullOrWhiteSpace(x)))
        {
            var relativePath = NormalizeRelativePath(inputPath);
            var fullPath = ResolvePhysicalPath(relativePath);

            if (fullPath is null || !File.Exists(fullPath) || map.ContainsKey(fullPath))
            {
                continue;
            }

            map[fullPath] = relativePath;
        }

        return map;
    }

    private string? ResolvePhysicalPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(_environment.WebRootPath))
        {
            return null;
        }

        var normalized = NormalizeRelativePath(relativePath);
        var safeSegment = normalized.Replace('/', Path.DirectorySeparatorChar);

        try
        {
            return FileNameHelper.BuildSafePath(_environment.WebRootPath, safeSegment);
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeRelativePath(string path)
    {
        return path
            .Replace('\\', '/')
            .TrimStart('~')
            .TrimStart('/');
    }

    private static bool HasUsableEngineFeature(MultiEngineFaceResult result)
    {
        if (result.Engines is null || result.Engines.Count == 0)
        {
            return false;
        }

        return result.Engines.Values.Any(IsUsableEngineFeature);
    }

    private static bool IsUsableEngineFeature(MultiEngineFaceEngineResult? engine)
    {
        if (engine is null || !string.IsNullOrWhiteSpace(engine.Error))
        {
            return false;
        }

        if (string.Equals(engine.Kind, "vector", StringComparison.OrdinalIgnoreCase))
        {
            return IsValidVector(engine.Vector);
        }

        return !string.IsNullOrWhiteSpace(engine.HashHex) &&
               engine.Brightness.HasValue &&
               engine.Contrast.HasValue;
    }
}
