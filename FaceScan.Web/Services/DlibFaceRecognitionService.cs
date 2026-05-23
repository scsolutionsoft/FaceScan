using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Models.Enums;
using FaceScan.Web.Models.Options;
using FaceScan.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FaceScan.Web.Services;

public class DlibFaceRecognitionService : IFaceRecognitionService
{
    private const string ProviderName = "face_recognition_dlib";
    private const string EmbeddingVersion = "face-recognition-dlib-v1";
    private const int EmbeddingSize = 128;
    private const string StudentTemplateCacheKey = "dlib-face-templates:students";
    private const string TeacherTemplateCacheKey = "dlib-face-templates:teachers";

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
    private readonly MockFaceRecognitionService _fallbackService;
    private readonly FaceRecognitionOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly IMemoryCache _memoryCache;
    private readonly IFaceRecognitionLoadGate _loadGate;
    private readonly ILogger<DlibFaceRecognitionService> _logger;

    public DlibFaceRecognitionService(
        ApplicationDbContext dbContext,
        MockFaceRecognitionService fallbackService,
        IOptions<FaceRecognitionOptions> options,
        IWebHostEnvironment environment,
        IMemoryCache memoryCache,
        IFaceRecognitionLoadGate loadGate,
        ILogger<DlibFaceRecognitionService> logger)
    {
        _dbContext = dbContext;
        _fallbackService = fallbackService;
        _options = options.Value;
        _environment = environment;
        _memoryCache = memoryCache;
        _loadGate = loadGate;
        _logger = logger;
    }

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

        var extraction = await ExtractEmbeddingsAsync(selectedPaths);
        if (!extraction.Ok)
        {
            return await TryFallbackEnrollAsync(
                studentId,
                imagePaths,
                "ไม่สามารถประมวลผลรูปสำหรับลงทะเบียนได้",
                extraction);
        }

        var validResults = extraction.Results
            .Where(IsValidEmbedding)
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
                    details = extraction.Results.Select(x => new
                    {
                        x.ImagePath,
                        x.FaceCount,
                        x.QualityScore,
                        x.Error
                    })
                })
            };
        }

        var templateVector = BuildTemplateVector(validResults.Select(x => x.Encoding!).ToList());
        var averageQuality = validResults.Average(x => x.QualityScore);

        var payload = new DlibEmbeddingPayload
        {
            Version = EmbeddingVersion,
            TrainedAtUtc = DateTime.UtcNow,
            TemplateVector = templateVector.Select(x => Math.Round(x, 8)).ToList(),
            SampleCount = validResults.Count,
            DetectionModel = NormalizeDetectionModel(_options.DlibDetectionModel),
            EncodingModel = NormalizeEncodingModel(_options.DlibEncodingModel),
            NumJitters = Math.Clamp(_options.DlibNumJitters, 1, 50),
            UpsampleTimes = Math.Clamp(_options.DlibUpsampleTimes, 0, 4)
        };

        profile.EnrollmentStatus = EnrollmentStatus.Ready;
        profile.TemplateVersion = EmbeddingVersion;
        profile.LastTrainedAt = DateTime.UtcNow;
        profile.EmbeddingJson = JsonSerializer.Serialize(payload);
        profile.QualityNote = $"ลงทะเบียนรูปแล้ว {validResults.Count} รูป, คุณภาพเฉลี่ย {(averageQuality * 100):0.#}%";

        await _dbContext.SaveChangesAsync();
        InvalidateStudentTemplateCache();

        return new FaceEnrollResult
        {
            Success = true,
            Message = "ลงทะเบียนใบหน้าเรียบร้อย",
            Provider = ProviderName,
            QualityScore = Math.Round((decimal)averageQuality, 4),
            RawResponseJson = JsonSerializer.Serialize(new
            {
                status = "ready",
                validImageCount = validResults.Count,
                requestedImageCount = selectedPaths.Count,
                templateVersion = EmbeddingVersion,
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

        var extraction = await ExtractEmbeddingsAsync(selectedPaths);
        if (!extraction.Ok)
        {
            return await TryFallbackTeacherEnrollAsync(
                userId,
                imagePaths,
                "ไม่สามารถประมวลผลรูปสำหรับลงทะเบียนครูได้",
                extraction);
        }

        var validResults = extraction.Results
            .Where(IsValidEmbedding)
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
                    details = extraction.Results.Select(x => new
                    {
                        x.ImagePath,
                        x.FaceCount,
                        x.QualityScore,
                        x.Error
                    })
                })
            };
        }

        var templateVector = BuildTemplateVector(validResults.Select(x => x.Encoding!).ToList());
        var averageQuality = validResults.Average(x => x.QualityScore);

        var payload = new DlibEmbeddingPayload
        {
            Version = EmbeddingVersion,
            TrainedAtUtc = DateTime.UtcNow,
            TemplateVector = templateVector.Select(x => Math.Round(x, 8)).ToList(),
            SampleCount = validResults.Count,
            DetectionModel = NormalizeDetectionModel(_options.DlibDetectionModel),
            EncodingModel = NormalizeEncodingModel(_options.DlibEncodingModel),
            NumJitters = Math.Clamp(_options.DlibNumJitters, 1, 50),
            UpsampleTimes = Math.Clamp(_options.DlibUpsampleTimes, 0, 4)
        };

        profile.EnrollmentStatus = EnrollmentStatus.Ready;
        profile.TemplateVersion = EmbeddingVersion;
        profile.LastTrainedAt = DateTime.UtcNow;
        profile.EmbeddingJson = JsonSerializer.Serialize(payload);
        profile.QualityNote = $"ลงทะเบียนรูปแล้ว {validResults.Count} รูป, คุณภาพเฉลี่ย {(averageQuality * 100):0.#}%";

        await _dbContext.SaveChangesAsync();
        InvalidateTeacherTemplateCache();

        return new FaceEnrollResult
        {
            Success = true,
            Message = "ลงทะเบียนใบหน้าครูเรียบร้อย",
            Provider = ProviderName,
            QualityScore = Math.Round((decimal)averageQuality, 4),
            RawResponseJson = JsonSerializer.Serialize(new
            {
                status = "ready",
                validImageCount = validResults.Count,
                requestedImageCount = selectedPaths.Count,
                templateVersion = EmbeddingVersion,
                averageQuality = Math.Round(averageQuality, 4)
            })
        };
    }

    public async Task<FaceMatchResult> VerifyAsync(Stream imageStream, string? recognitionProfile = null)
    {
        await using var rawMemory = new MemoryStream();
        await imageStream.CopyToAsync(rawMemory);

        var rawBytes = rawMemory.ToArray();
        if (rawBytes.Length < 1500)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = "คุณภาพรูปต่ำเกินไป",
                Provider = ProviderName,
                ConfidenceScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new { error = "image_too_small" })
            };
        }

        var probeBytes = rawBytes;
        if (probeBytes.Length < 1500)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = "ข้อมูลภาพสแกนไม่ถูกต้อง",
                Provider = ProviderName,
                ConfidenceScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new { error = "invalid_payload" })
            };
        }

        var probeExtraction = await ExtractProbeAsync(probeBytes);
        if (!probeExtraction.Ok)
        {
            if (IsFaceQualityExtractionError(probeExtraction.Error))
            {
                return BuildLowQualityFaceMatchResult(probeExtraction.Error);
            }

            return await TryFallbackVerifyAsync(
                probeBytes,
                "ไม่สามารถประมวลผลภาพใบหน้าได้",
                probeExtraction);
        }

        var probe = probeExtraction.Results.FirstOrDefault();
        if (probe is not null && IsLowQualityProbe(probe))
        {
            return BuildLowQualityFaceMatchResult("face_roi_low_quality", probe.FaceCount, probe.QualityScore);
        }

        if (!IsValidEmbedding(probe) || probe?.Encoding is null)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = "ไม่พบใบหน้าอย่างชัดเจน",
                Provider = ProviderName,
                ConfidenceScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new
                {
                    error = "face_not_detected",
                    faceCount = probe?.FaceCount ?? 0,
                    detail = probe?.Error
                })
            };
        }

        var probeVector = probe.Encoding;

        var candidates = await GetStudentCandidatesAsync();

        if (candidates.Count == 0)
        {
            return await TryFallbackVerifyByReasonAsync(
                probeBytes,
                "ไม่พบโปรไฟล์ที่ลงทะเบียนไว้",
                "no_profile");
        }

        CandidateMatch? bestMatch = null;
        foreach (var candidate in candidates)
        {
            var distance = CalculateDistance(probeVector, candidate.TemplateVector);
            if (double.IsNaN(distance) || double.IsInfinity(distance))
            {
                continue;
            }

            if (bestMatch is null || distance < bestMatch.Distance)
            {
                bestMatch = new CandidateMatch(candidate, distance);
            }
        }

        if (bestMatch is null)
        {
            return await TryFallbackVerifyByReasonAsync(
                probeBytes,
                "ไม่พบเทมเพลตใบหน้าที่ใช้งานได้",
                "no_valid_template");
        }

        var threshold = Math.Clamp((double)_options.DlibDistanceThreshold, 0.25d, 1.0d);
        var confidence = BuildConfidence(bestMatch.Distance, threshold);

        if (bestMatch.Distance > threshold)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = "ไม่พบรายการที่ตรงกัน",
                Provider = ProviderName,
                ConfidenceScore = confidence,
                RawResponseJson = JsonSerializer.Serialize(new
                {
                    error = "below_threshold",
                    distance = Math.Round(bestMatch.Distance, 6),
                    threshold = Math.Round(threshold, 6),
                    confidence
                })
            };
        }

        return new FaceMatchResult
        {
            Success = true,
            StudentId = bestMatch.Student.StudentId,
            StudentCode = bestMatch.Student.StudentCode,
            StudentName = bestMatch.Student.StudentName,
            ConfidenceScore = confidence,
            Provider = ProviderName,
            Message = "ยืนยันตัวตนสำเร็จ",
            RawResponseJson = JsonSerializer.Serialize(new
            {
                match = true,
                studentCode = bestMatch.Student.StudentCode,
                distance = Math.Round(bestMatch.Distance, 6),
                confidence
            })
        };
    }

    public async Task<FaceMatchResult> VerifyTeacherAsync(Stream imageStream, string? recognitionProfile = null)
    {
        await using var rawMemory = new MemoryStream();
        await imageStream.CopyToAsync(rawMemory);

        var rawBytes = rawMemory.ToArray();
        if (rawBytes.Length < 1500)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = "คุณภาพรูปต่ำเกินไป",
                Provider = ProviderName,
                ConfidenceScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new { error = "image_too_small" })
            };
        }

        var probeBytes = rawBytes;
        if (probeBytes.Length < 1500)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = "ข้อมูลภาพสแกนไม่ถูกต้อง",
                Provider = ProviderName,
                ConfidenceScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new { error = "invalid_payload" })
            };
        }

        var probeExtraction = await ExtractProbeAsync(probeBytes);
        if (!probeExtraction.Ok)
        {
            if (IsFaceQualityExtractionError(probeExtraction.Error))
            {
                return BuildLowQualityFaceMatchResult(probeExtraction.Error);
            }

            return await TryFallbackTeacherVerifyAsync(
                probeBytes,
                "ไม่สามารถประมวลผลภาพใบหน้าครูได้",
                probeExtraction);
        }

        var probe = probeExtraction.Results.FirstOrDefault();
        if (probe is not null && IsLowQualityProbe(probe))
        {
            return BuildLowQualityFaceMatchResult("face_roi_low_quality", probe.FaceCount, probe.QualityScore);
        }

        if (!IsValidEmbedding(probe) || probe?.Encoding is null)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = "ไม่พบใบหน้าอย่างชัดเจน",
                Provider = ProviderName,
                ConfidenceScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new
                {
                    error = "face_not_detected",
                    faceCount = probe?.FaceCount ?? 0,
                    detail = probe?.Error
                })
            };
        }

        var probeVector = probe.Encoding;

        var candidates = await GetTeacherCandidatesAsync();

        if (candidates.Count == 0)
        {
            return await TryFallbackTeacherVerifyByReasonAsync(
                probeBytes,
                "ไม่พบโปรไฟล์ครูที่ลงทะเบียนไว้",
                "no_profile");
        }

        TeacherCandidateMatch? bestMatch = null;
        foreach (var candidate in candidates)
        {
            var distance = CalculateDistance(probeVector, candidate.TemplateVector);
            if (double.IsNaN(distance) || double.IsInfinity(distance))
            {
                continue;
            }

            if (bestMatch is null || distance < bestMatch.Distance)
            {
                bestMatch = new TeacherCandidateMatch(candidate, distance);
            }
        }

        if (bestMatch is null)
        {
            return await TryFallbackTeacherVerifyByReasonAsync(
                probeBytes,
                "ไม่พบเทมเพลตใบหน้าครูที่ใช้งานได้",
                "no_valid_template");
        }

        var threshold = Math.Clamp((double)_options.DlibDistanceThreshold, 0.25d, 1.0d);
        var confidence = BuildConfidence(bestMatch.Distance, threshold);

        if (bestMatch.Distance > threshold)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = "ไม่พบรายการที่ตรงกัน",
                Provider = ProviderName,
                ConfidenceScore = confidence,
                RawResponseJson = JsonSerializer.Serialize(new
                {
                    error = "below_threshold",
                    distance = Math.Round(bestMatch.Distance, 6),
                    threshold = Math.Round(threshold, 6),
                    confidence
                })
            };
        }

        return new FaceMatchResult
        {
            Success = true,
            UserId = bestMatch.User.UserId,
            UserName = bestMatch.User.UserName,
            FullName = bestMatch.User.FullName,
            StudentCode = bestMatch.User.UserName,
            StudentName = bestMatch.User.FullName,
            ConfidenceScore = confidence,
            Provider = ProviderName,
            Message = "ยืนยันตัวตนสำเร็จ",
            RawResponseJson = JsonSerializer.Serialize(new
            {
                match = true,
                userName = bestMatch.User.UserName,
                distance = Math.Round(bestMatch.Distance, 6),
                confidence
            })
        };
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

    private async Task<FaceEnrollResult> TryFallbackEnrollAsync(
        int studentId,
        List<string> imagePaths,
        string message,
        PythonExtractResponse extraction)
    {
        _logger.LogWarning(
            "Dlib enrollment extraction failed. studentId: {StudentId}, error: {Error}",
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
                    error = "dlib_extract_failed",
                    detail = extraction.Error ?? "unknown_error"
                })
            };
        }

        var fallbackResult = await _fallbackService.EnrollStudentAsync(studentId, imagePaths);
        var fallbackMessage = $"{fallbackResult.Message} (สลับไปใช้โหมดสำรองอัตโนมัติจาก Dlib)";

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
                primaryError = extraction.Error ?? "unknown_error",
                fallbackProvider = fallbackResult.Provider,
                fallbackSuccess = fallbackResult.Success,
                fallbackRaw = fallbackResult.RawResponseJson
            })
        };
    }

    private async Task<FaceEnrollResult> TryFallbackTeacherEnrollAsync(
        string userId,
        List<string> imagePaths,
        string message,
        PythonExtractResponse extraction)
    {
        _logger.LogWarning(
            "Dlib teacher enrollment extraction failed. userId: {UserId}, error: {Error}",
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
                    error = "dlib_extract_failed",
                    detail = extraction.Error ?? "unknown_error"
                })
            };
        }

        var fallbackResult = await _fallbackService.EnrollTeacherAsync(userId, imagePaths);
        var fallbackMessage = $"{fallbackResult.Message} (สลับไปใช้โหมดสำรองอัตโนมัติจาก Dlib)";

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
                primaryError = extraction.Error ?? "unknown_error",
                fallbackProvider = fallbackResult.Provider,
                fallbackSuccess = fallbackResult.Success,
                fallbackRaw = fallbackResult.RawResponseJson
            })
        };
    }

    private async Task<FaceMatchResult> TryFallbackVerifyAsync(
        byte[] probeBytes,
        string message,
        PythonExtractResponse extraction)
    {
        if (IsFaceQualityExtractionError(extraction.Error))
        {
            return BuildLowQualityFaceMatchResult(extraction.Error);
        }

        _logger.LogWarning(
            "Dlib verify extraction failed. error: {Error}",
            extraction.Error ?? "unknown_error");

        if (!_options.DlibAutoFallbackToMock)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = message,
                Provider = ProviderName,
                ConfidenceScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new
                {
                    error = "dlib_extract_failed",
                    detail = extraction.Error ?? "unknown_error"
                })
            };
        }

        await using var fallbackStream = new MemoryStream(probeBytes, writable: false);
        var fallbackResult = await _fallbackService.VerifyAsync(fallbackStream);
        var fallbackMessage = $"{fallbackResult.Message} (สลับไปใช้โหมดสำรองอัตโนมัติจาก Dlib)";

        return new FaceMatchResult
        {
            Success = fallbackResult.Success,
            StudentId = fallbackResult.StudentId,
            StudentCode = fallbackResult.StudentCode,
            StudentName = fallbackResult.StudentName,
            ConfidenceScore = fallbackResult.ConfidenceScore,
            Provider = fallbackResult.Provider,
            Message = fallbackMessage,
            RawResponseJson = JsonSerializer.Serialize(new
            {
                fallback = true,
                primaryProvider = ProviderName,
                primaryError = extraction.Error ?? "unknown_error",
                fallbackProvider = fallbackResult.Provider,
                fallbackSuccess = fallbackResult.Success,
                fallbackRaw = fallbackResult.RawResponseJson
            })
        };
    }

    private async Task<FaceMatchResult> TryFallbackTeacherVerifyAsync(
        byte[] probeBytes,
        string message,
        PythonExtractResponse extraction)
    {
        if (IsFaceQualityExtractionError(extraction.Error))
        {
            return BuildLowQualityFaceMatchResult(extraction.Error);
        }

        _logger.LogWarning(
            "Dlib teacher verify extraction failed. error: {Error}",
            extraction.Error ?? "unknown_error");

        if (!_options.DlibAutoFallbackToMock)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = message,
                Provider = ProviderName,
                ConfidenceScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new
                {
                    error = "dlib_extract_failed",
                    detail = extraction.Error ?? "unknown_error"
                })
            };
        }

        await using var fallbackStream = new MemoryStream(probeBytes, writable: false);
        var fallbackResult = await _fallbackService.VerifyTeacherAsync(fallbackStream);
        var fallbackMessage = $"{fallbackResult.Message} (สลับไปใช้โหมดสำรองอัตโนมัติจาก Dlib)";

        return new FaceMatchResult
        {
            Success = fallbackResult.Success,
            UserId = fallbackResult.UserId,
            UserName = fallbackResult.UserName,
            FullName = fallbackResult.FullName,
            StudentCode = fallbackResult.StudentCode,
            StudentName = fallbackResult.StudentName,
            ConfidenceScore = fallbackResult.ConfidenceScore,
            Provider = fallbackResult.Provider,
            Message = fallbackMessage,
            RawResponseJson = JsonSerializer.Serialize(new
            {
                fallback = true,
                primaryProvider = ProviderName,
                primaryError = extraction.Error ?? "unknown_error",
                fallbackProvider = fallbackResult.Provider,
                fallbackSuccess = fallbackResult.Success,
                fallbackRaw = fallbackResult.RawResponseJson
            })
        };
    }

    private async Task<FaceMatchResult> TryFallbackVerifyByReasonAsync(
        byte[] probeBytes,
        string message,
        string reason)
    {
        _logger.LogWarning(
            "Dlib verify cannot continue. reason: {Reason}",
            reason);

        if (!_options.DlibAutoFallbackToMock)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = message,
                Provider = ProviderName,
                ConfidenceScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new
                {
                    error = reason
                })
            };
        }

        await using var fallbackStream = new MemoryStream(probeBytes, writable: false);
        var fallbackResult = await _fallbackService.VerifyAsync(fallbackStream);
        var fallbackMessage = $"{fallbackResult.Message} (สลับไปใช้โหมดสำรองอัตโนมัติจาก Dlib)";

        return new FaceMatchResult
        {
            Success = fallbackResult.Success,
            StudentId = fallbackResult.StudentId,
            StudentCode = fallbackResult.StudentCode,
            StudentName = fallbackResult.StudentName,
            ConfidenceScore = fallbackResult.ConfidenceScore,
            Provider = fallbackResult.Provider,
            Message = fallbackMessage,
            RawResponseJson = JsonSerializer.Serialize(new
            {
                fallback = true,
                primaryProvider = ProviderName,
                primaryError = reason,
                fallbackProvider = fallbackResult.Provider,
                fallbackSuccess = fallbackResult.Success,
                fallbackRaw = fallbackResult.RawResponseJson
            })
        };
    }

    private async Task<FaceMatchResult> TryFallbackTeacherVerifyByReasonAsync(
        byte[] probeBytes,
        string message,
        string reason)
    {
        _logger.LogWarning(
            "Dlib teacher verify cannot continue. reason: {Reason}",
            reason);

        if (!_options.DlibAutoFallbackToMock)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = message,
                Provider = ProviderName,
                ConfidenceScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new
                {
                    error = reason
                })
            };
        }

        await using var fallbackStream = new MemoryStream(probeBytes, writable: false);
        var fallbackResult = await _fallbackService.VerifyTeacherAsync(fallbackStream);
        var fallbackMessage = $"{fallbackResult.Message} (สลับไปใช้โหมดสำรองอัตโนมัติจาก Dlib)";

        return new FaceMatchResult
        {
            Success = fallbackResult.Success,
            UserId = fallbackResult.UserId,
            UserName = fallbackResult.UserName,
            FullName = fallbackResult.FullName,
            StudentCode = fallbackResult.StudentCode,
            StudentName = fallbackResult.StudentName,
            ConfidenceScore = fallbackResult.ConfidenceScore,
            Provider = fallbackResult.Provider,
            Message = fallbackMessage,
            RawResponseJson = JsonSerializer.Serialize(new
            {
                fallback = true,
                primaryProvider = ProviderName,
                primaryError = reason,
                fallbackProvider = fallbackResult.Provider,
                fallbackSuccess = fallbackResult.Success,
                fallbackRaw = fallbackResult.RawResponseJson
            })
        };
    }

    private Dictionary<string, string> BuildPhysicalPathMap(IEnumerable<string> imagePaths)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var inputPath in imagePaths.Where(x => !string.IsNullOrWhiteSpace(x)))
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

    private async Task<PythonExtractResponse> ExtractProbeAsync(byte[] imageBytes)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"facescan-probe-{Guid.NewGuid():N}.jpg");
        await File.WriteAllBytesAsync(tempPath, imageBytes);

        try
        {
            return await ExtractEmbeddingsAsync(new[] { tempPath });
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

    private async Task<PythonExtractResponse> ExtractEmbeddingsAsync(IReadOnlyList<string> imagePaths)
    {
        var request = new PythonExtractRequest
        {
            Mode = "extract",
            ImagePaths = imagePaths.ToList(),
            DetectionModel = NormalizeDetectionModel(_options.DlibDetectionModel),
            EncodingModel = NormalizeEncodingModel(_options.DlibEncodingModel),
            NumJitters = Math.Clamp(_options.DlibNumJitters, 1, 50),
            UpsampleTimes = Math.Clamp(_options.DlibUpsampleTimes, 0, 4),
            MinFaceQualityScore = Math.Clamp(_options.DlibMinFaceQualityScore, 0.05m, 0.85m)
        };

        return await InvokePythonAsync(request);
    }

    private async Task<PythonExtractResponse> InvokePythonAsync(PythonExtractRequest request)
    {
        using var lease = await _loadGate.EnterAsync();

        var scriptPath = ResolveScriptPath();
        if (!File.Exists(scriptPath))
        {
            return new PythonExtractResponse
            {
                Ok = false,
                Error = $"Dlib script was not found at: {scriptPath}"
            };
        }

        var requestJson = JsonSerializer.Serialize(request);
        var executables = BuildPythonExecutableCandidates();

        PythonExtractResponse? lastFailure = null;
        foreach (var executable in executables)
        {
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

        return lastFailure ?? new PythonExtractResponse
        {
            Ok = false,
            Error = "ไม่สามารถเริ่มต้นโปรเซส Python ได้"
        };
    }

    private async Task<PythonExtractResponse> InvokePythonWithExecutableAsync(
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
                return new PythonExtractResponse
                {
                    Ok = false,
                    Error = $"ไม่สามารถเริ่มต้นโปรเซส Python ด้วย executable '{pythonExecutable}' ได้"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ไม่สามารถเริ่มต้นโปรเซส Python ด้วย executable '{Executable}' ได้", pythonExecutable);
            return new PythonExtractResponse
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

            return new PythonExtractResponse
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
                "Dlib process exited with code {ExitCode}. executable: {Executable}, stderr: {Stderr}",
                process.ExitCode,
                pythonExecutable,
                TrimForLog(stderr));

            return new PythonExtractResponse
            {
                Ok = false,
                Error = $"Python process exited with code {process.ExitCode}.",
                RawStdout = TrimForLog(stdout),
                RawStderr = TrimForLog(stderr)
            };
        }

        if (string.IsNullOrWhiteSpace(stdout))
        {
            return new PythonExtractResponse
            {
                Ok = false,
                Error = "Python process returned empty output.",
                RawStderr = TrimForLog(stderr)
            };
        }

        try
        {
            var response = JsonSerializer.Deserialize<PythonExtractResponse>(stdout, JsonOptions);
            if (response is null)
            {
                return new PythonExtractResponse
                {
                    Ok = false,
                    Error = "รูปแบบผลลัพธ์จากสคริปต์ dlib ไม่ถูกต้อง",
                    RawStdout = TrimForLog(stdout),
                    RawStderr = TrimForLog(stderr)
                };
            }

            response.Results ??= new List<PythonFaceResult>();
            response.RawStdout ??= TrimForLog(stdout);
            response.RawStderr ??= string.IsNullOrWhiteSpace(stderr) ? null : TrimForLog(stderr);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ไม่สามารถแปลงผลลัพธ์จากสคริปต์ dlib ได้");

            return new PythonExtractResponse
            {
                Ok = false,
                Error = $"ไม่สามารถแปลงผลลัพธ์ dlib ได้: {ex.Message}",
                RawStdout = TrimForLog(stdout),
                RawStderr = TrimForLog(stderr)
            };
        }
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

    private static bool IsExecutableNotFound(PythonExtractResponse response)
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

    private string ResolveScriptPath()
    {
        if (Path.IsPathRooted(_options.DlibScriptPath))
        {
            return _options.DlibScriptPath;
        }

        return Path.Combine(_environment.ContentRootPath, _options.DlibScriptPath.Replace('/', Path.DirectorySeparatorChar));
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

    private static bool TryParseTemplateVector(string? embeddingJson, out double[] vector)
    {
        vector = Array.Empty<double>();

        if (string.IsNullOrWhiteSpace(embeddingJson))
        {
            return false;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<DlibEmbeddingPayload>(embeddingJson, JsonOptions);
            if (payload?.TemplateVector is null || payload.TemplateVector.Count != EmbeddingSize)
            {
                return false;
            }

            if (payload.TemplateVector.Any(x => double.IsNaN(x) || double.IsInfinity(x)))
            {
                return false;
            }

            vector = payload.TemplateVector.ToArray();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidEmbedding(PythonFaceResult? result)
    {
        if (result?.Encoding is null || result.Encoding.Count != EmbeddingSize)
        {
            return false;
        }

        return result.Encoding.All(x => !double.IsNaN(x) && !double.IsInfinity(x));
    }

    private static bool IsFaceQualityExtractionError(string? error)
    {
        return string.Equals(error, "face_roi_low_quality", StringComparison.OrdinalIgnoreCase)
            || string.Equals(error, "no_face_detected", StringComparison.OrdinalIgnoreCase)
            || string.Equals(error, "invalid_face_crop", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsLowQualityProbe(PythonFaceResult probe)
    {
        var threshold = Math.Clamp((double)_options.DlibMinFaceQualityScore, 0.05d, 0.85d);
        return probe.QualityScore > 0 && probe.QualityScore < threshold;
    }

    private FaceMatchResult BuildLowQualityFaceMatchResult(string? error, int faceCount = 0, double qualityScore = 0)
    {
        return new FaceMatchResult
        {
            Success = false,
            Message = "ภาพใบหน้าชัดไม่พอสำหรับการยืนยัน",
            Provider = ProviderName,
            ConfidenceScore = 0m,
            RawResponseJson = JsonSerializer.Serialize(new
            {
                error = error ?? "face_roi_low_quality",
                faceCount,
                qualityScore = Math.Round(qualityScore, 6),
                minQualityScore = Math.Round((double)_options.DlibMinFaceQualityScore, 6)
            })
        };
    }

    private static double[] BuildTemplateVector(IReadOnlyList<List<double>> embeddings)
    {
        var template = new double[EmbeddingSize];

        foreach (var embedding in embeddings)
        {
            for (var index = 0; index < EmbeddingSize; index++)
            {
                template[index] += embedding[index];
            }
        }

        if (embeddings.Count == 0)
        {
            return template;
        }

        for (var index = 0; index < EmbeddingSize; index++)
        {
            template[index] /= embeddings.Count;
        }

        return template;
    }

    private static double CalculateDistance(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        if (left.Count != EmbeddingSize || right.Count != EmbeddingSize)
        {
            return double.PositiveInfinity;
        }

        var sum = 0d;
        for (var index = 0; index < EmbeddingSize; index++)
        {
            var diff = left[index] - right[index];
            sum += diff * diff;
        }

        return Math.Sqrt(sum);
    }

    private static decimal BuildConfidence(double distance, double threshold)
    {
        var span = Math.Max(0.25d, threshold * 2d);
        var normalized = 1d - (Math.Min(distance, span) / span);
        var value = 0.70d + (0.29d * normalized);
        return Math.Round((decimal)Math.Clamp(value, 0d, 0.99d), 4);
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

    private async Task<IReadOnlyList<CachedStudentCandidate>> GetStudentCandidatesAsync()
    {
        if (_memoryCache.TryGetValue<IReadOnlyList<CachedStudentCandidate>>(StudentTemplateCacheKey, out var cached) &&
            cached is not null)
        {
            return cached;
        }

        var rows = await _dbContext.Students
            .AsNoTracking()
            .Where(x => x.IsActive &&
                        x.FaceProfile != null &&
                        x.FaceProfile.EnrollmentStatus == EnrollmentStatus.Ready &&
                        x.FaceProfile.EmbeddingJson != null)
            .Select(x => new
            {
                StudentId = x.Id,
                x.StudentCode,
                x.Prefix,
                x.FirstName,
                x.LastName,
                EmbeddingJson = x.FaceProfile!.EmbeddingJson!
            })
            .ToListAsync();

        var candidates = new List<CachedStudentCandidate>(rows.Count);
        foreach (var row in rows)
        {
            if (!TryParseTemplateVector(row.EmbeddingJson, out var templateVector))
            {
                continue;
            }

            candidates.Add(new CachedStudentCandidate(
                row.StudentId,
                row.StudentCode,
                $"{row.Prefix}{row.FirstName} {row.LastName}".Trim(),
                templateVector));
        }

        _memoryCache.Set(StudentTemplateCacheKey, candidates, TemplateCacheOptions);
        return candidates;
    }

    private async Task<IReadOnlyList<CachedTeacherCandidate>> GetTeacherCandidatesAsync()
    {
        if (_memoryCache.TryGetValue<IReadOnlyList<CachedTeacherCandidate>>(TeacherTemplateCacheKey, out var cached) &&
            cached is not null)
        {
            return cached;
        }

        var rows = await (
            from user in _dbContext.Users.AsNoTracking()
            join profile in _dbContext.TeacherFaceProfiles.AsNoTracking() on user.Id equals profile.UserId
            join userRole in _dbContext.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in _dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where user.IsActive &&
                  role.Name != null &&
                  TeacherRoleCatalog.AttendanceRoles.Contains(role.Name) &&
                  profile.EnrollmentStatus == EnrollmentStatus.Ready &&
                  profile.EmbeddingJson != null
            select new
            {
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                user.FullName,
                EmbeddingJson = profile.EmbeddingJson!
            })
            .ToListAsync();

        var candidates = new List<CachedTeacherCandidate>();
        foreach (var row in rows
                     .GroupBy(x => x.UserId, StringComparer.OrdinalIgnoreCase)
                     .Select(x => x.First()))
        {
            if (!TryParseTemplateVector(row.EmbeddingJson, out var templateVector))
            {
                continue;
            }

            candidates.Add(new CachedTeacherCandidate(
                row.UserId,
                row.UserName,
                row.FullName,
                templateVector));
        }

        _memoryCache.Set(TeacherTemplateCacheKey, candidates, TemplateCacheOptions);
        return candidates;
    }

    private void InvalidateStudentTemplateCache()
    {
        _memoryCache.Remove(StudentTemplateCacheKey);
    }

    private void InvalidateTeacherTemplateCache()
    {
        _memoryCache.Remove(TeacherTemplateCacheKey);
    }

    private sealed record CandidateMatch(CachedStudentCandidate Student, double Distance);
    private sealed record TeacherCandidateMatch(CachedTeacherCandidate User, double Distance);
    private sealed record CachedStudentCandidate(int StudentId, string StudentCode, string StudentName, double[] TemplateVector);
    private sealed record CachedTeacherCandidate(string UserId, string UserName, string FullName, double[] TemplateVector);

    private sealed class DlibEmbeddingPayload
    {
        public string Version { get; set; } = EmbeddingVersion;
        public DateTime TrainedAtUtc { get; set; }
        public List<double> TemplateVector { get; set; } = new();
        public int SampleCount { get; set; }
        public string DetectionModel { get; set; } = "hog";
        public string EncodingModel { get; set; } = "small";
        public int NumJitters { get; set; }
        public int UpsampleTimes { get; set; }
    }

    private sealed class PythonExtractRequest
    {
        public string Mode { get; set; } = "extract";
        public List<string> ImagePaths { get; set; } = new();
        public string DetectionModel { get; set; } = "hog";
        public string EncodingModel { get; set; } = "small";
        public int NumJitters { get; set; } = 1;
        public int UpsampleTimes { get; set; } = 1;
        public decimal MinFaceQualityScore { get; set; } = 0.32m;
    }

    private sealed class PythonExtractResponse
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public List<PythonFaceResult> Results { get; set; } = new();
        public string? RawStdout { get; set; }
        public string? RawStderr { get; set; }
    }

    private sealed class PythonFaceResult
    {
        public string ImagePath { get; set; } = string.Empty;
        public int FaceCount { get; set; }
        public double QualityScore { get; set; }
        public List<double>? Encoding { get; set; }
        public string? Error { get; set; }
    }
}


