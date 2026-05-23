using System.Text.Json;
using FaceScan.Web.Models;
using FaceScan.Web.Models.Entities;

namespace FaceScan.Web.Services;

public sealed partial class HybridFaceRecognitionService
{
    public async Task<FaceMatchResult> VerifyAsync(Stream imageStream, string? recognitionProfile = null)
    {
        var normalizedProfile = NormalizeRecognitionProfile(recognitionProfile);

        await using var rawMemory = new MemoryStream();
        await imageStream.CopyToAsync(rawMemory);
        var probeBytes = rawMemory.ToArray();

        if (probeBytes.Length < 1500)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = "ใบหน้าไม่ชัด",
                Provider = ProviderName,
                ConfidenceScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new { error = "low_quality", reason = "image_too_small" })
            };
        }

        var plan = BuildScanEnginePlan(normalizedProfile);
        var extraction = await _pythonClient.ExtractProbeAsync(probeBytes, normalizedProfile, plan.RequestedEngines);
        if (!extraction.Ok)
        {
            if (IsLowQualityExtraction(extraction))
            {
                return BuildLowQualityFaceMatchResult(probeBytes, normalizedProfile, extraction);
            }

            return await TryFallbackVerifyAsync(
                probeBytes,
                "ไม่สามารถประมวลผลข้อมูลใบหน้าได้",
                extraction,
                normalizedProfile);
        }

        var probeFeatures = BuildProbeFeatures(extraction);
        if (probeFeatures.Count == 0)
        {
            return await TryFallbackVerifyByReasonAsync(
                probeBytes,
                "ไม่พบใบหน้าที่พร้อมใช้งานในภาพที่สแกน",
                "no_usable_engine_feature",
                normalizedProfile);
        }

        var candidates = await GetStudentCandidatesAsync();
        if (candidates.Count == 0)
        {
            return await TryFallbackVerifyByReasonAsync(
                probeBytes,
                "ไม่พบข้อมูลลงทะเบียนใบหน้าที่พร้อมใช้งาน",
                "no_candidate_template",
                normalizedProfile);
        }

        var matches = candidates
            .Select(x => new
            {
                Candidate = x,
                Evaluation = EvaluateCandidateMatch(x.Engines, probeFeatures, plan)
            })
            .Where(x => x.Evaluation is not null)
            .Select(x => new CandidateMatch(x.Candidate, x.Evaluation!))
            .OrderByDescending(x => x.Evaluation.OverallScore)
            .ToList();

        if (matches.Count == 0 || matches[0].Evaluation.OverallScore <= 0d)
        {
            return await TryFallbackVerifyByReasonAsync(
                probeBytes,
                "ไม่พบข้อมูลที่ตรงกัน",
                "no_match",
                normalizedProfile);
        }

        if (IsAmbiguousMatch(matches))
        {
            return await TryFallbackVerifyByReasonAsync(
                probeBytes,
                "พบใบหน้าคล้ายกันหลายรายการ ระบบจึงไม่ยืนยันผลอัตโนมัติ",
                "ambiguous_match",
                normalizedProfile);
        }

        var bestMatch = matches[0];
        var provider = BuildProviderLabel(bestMatch.Evaluation.MatchedEngines.Select(x => x.Engine));

        return new FaceMatchResult
        {
            Success = true,
            StudentId = bestMatch.Candidate.StudentId,
            StudentCode = bestMatch.Candidate.StudentCode,
            StudentName = bestMatch.Candidate.StudentName,
            ConfidenceScore = (decimal)BuildConfidence(bestMatch.Evaluation.OverallScore),
            Provider = provider,
            Message = "ยืนยันตัวตนสำเร็จ",
            RawResponseJson = JsonSerializer.Serialize(new
            {
                match = true,
                studentCode = bestMatch.Candidate.StudentCode,
                recognitionProfile = normalizedProfile,
                engines = bestMatch.Evaluation.MatchedEngines.Select(x => new
                {
                    x.Engine,
                    score = Math.Round(x.Score, 4),
                    threshold = Math.Round(x.Threshold, 4),
                    raw = Math.Round(x.RawValue, 4),
                    metric = x.Metric
                }),
                availableProbeEngines = extraction.AvailableEngines,
                overallScore = Math.Round(bestMatch.Evaluation.OverallScore, 4)
            })
        };
    }

    public async Task<FaceMatchResult> VerifyTeacherAsync(Stream imageStream, string? recognitionProfile = null)
    {
        var normalizedProfile = NormalizeRecognitionProfile(recognitionProfile);

        await using var rawMemory = new MemoryStream();
        await imageStream.CopyToAsync(rawMemory);
        var probeBytes = rawMemory.ToArray();

        if (probeBytes.Length < 1500)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = "ใบหน้าไม่ชัด",
                Provider = ProviderName,
                ConfidenceScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new { error = "low_quality", reason = "image_too_small" })
            };
        }

        var plan = BuildScanEnginePlan(normalizedProfile);
        var extraction = await _pythonClient.ExtractProbeAsync(probeBytes, normalizedProfile, plan.RequestedEngines);
        if (!extraction.Ok)
        {
            if (IsLowQualityExtraction(extraction))
            {
                return BuildLowQualityFaceMatchResult(probeBytes, normalizedProfile, extraction, isTeacher: true);
            }

            return await TryFallbackTeacherVerifyAsync(
                probeBytes,
                "ไม่สามารถประมวลผลข้อมูลใบหน้าครูได้",
                extraction,
                normalizedProfile);
        }

        var probeFeatures = BuildProbeFeatures(extraction);
        if (probeFeatures.Count == 0)
        {
            if (IsLowQualityExtraction(extraction))
            {
                return BuildLowQualityFaceMatchResult(probeBytes, normalizedProfile, extraction, isTeacher: true);
            }

            return await TryFallbackTeacherVerifyByReasonAsync(
                probeBytes,
                "ไม่พบใบหน้าที่พร้อมใช้งานในภาพที่สแกน",
                "no_usable_engine_feature",
                normalizedProfile);
        }

        var candidates = await GetTeacherCandidatesAsync();
        if (candidates.Count == 0)
        {
            return await TryFallbackTeacherVerifyByReasonAsync(
                probeBytes,
                "ไม่พบข้อมูลลงทะเบียนใบหน้าครูที่พร้อมใช้งาน",
                "no_candidate_template",
                normalizedProfile);
        }

        var matches = candidates
            .Select(x => new
            {
                Candidate = x,
                Evaluation = EvaluateCandidateMatch(x.Engines, probeFeatures, plan)
            })
            .Where(x => x.Evaluation is not null)
            .Select(x => new TeacherCandidateMatch(x.Candidate, x.Evaluation!))
            .OrderByDescending(x => x.Evaluation.OverallScore)
            .ToList();

        if (matches.Count == 0 || matches[0].Evaluation.OverallScore <= 0d)
        {
            return await TryFallbackTeacherVerifyByReasonAsync(
                probeBytes,
                "ไม่พบข้อมูลที่ตรงกัน",
                "no_match",
                normalizedProfile);
        }

        if (IsAmbiguousTeacherMatch(matches))
        {
            return await TryFallbackTeacherVerifyByReasonAsync(
                probeBytes,
                "พบใบหน้าคล้ายกันหลายรายการ ระบบจึงไม่ยืนยันผลอัตโนมัติ",
                "ambiguous_match",
                normalizedProfile);
        }

        var bestMatch = matches[0];
        var provider = BuildProviderLabel(bestMatch.Evaluation.MatchedEngines.Select(x => x.Engine));

        return new FaceMatchResult
        {
            Success = true,
            UserId = bestMatch.Candidate.UserId,
            UserName = bestMatch.Candidate.UserName,
            FullName = bestMatch.Candidate.FullName,
            StudentCode = bestMatch.Candidate.UserName,
            StudentName = bestMatch.Candidate.FullName,
            ConfidenceScore = (decimal)BuildConfidence(bestMatch.Evaluation.OverallScore),
            Provider = provider,
            Message = "ยืนยันตัวตนครูสำเร็จ",
            RawResponseJson = JsonSerializer.Serialize(new
            {
                match = true,
                userName = bestMatch.Candidate.UserName,
                recognitionProfile = normalizedProfile,
                engines = bestMatch.Evaluation.MatchedEngines.Select(x => new
                {
                    x.Engine,
                    score = Math.Round(x.Score, 4),
                    threshold = Math.Round(x.Threshold, 4),
                    raw = Math.Round(x.RawValue, 4),
                    metric = x.Metric
                }),
                availableProbeEngines = extraction.AvailableEngines,
                overallScore = Math.Round(bestMatch.Evaluation.OverallScore, 4)
            })
        };
    }

    private FaceEnginePlan BuildScanEnginePlan(string recognitionProfile)
    {
        return recognitionProfile switch
        {
            FaceRecognitionProfiles.Fast => new FaceEnginePlan(
                [OpenCvLiteEngine, MockEngine, DlibEngine, InsightFaceEngine, DeepFaceEngine],
                1,
                true),
            FaceRecognitionProfiles.Accurate => new FaceEnginePlan(
                [InsightFaceEngine, DlibEngine, DeepFaceEngine, OpenCvLiteEngine, MockEngine],
                3,
                false),
            FaceRecognitionProfiles.Stable => new FaceEnginePlan(
                [OpenCvLiteEngine, DlibEngine, InsightFaceEngine, DeepFaceEngine, MockEngine],
                2,
                false),
            _ => new FaceEnginePlan(
                [OpenCvLiteEngine, DlibEngine, InsightFaceEngine, DeepFaceEngine, MockEngine],
                2,
                false)
        };
    }

    private async Task<FaceMatchResult> TryFallbackVerifyAsync(
        byte[] probeBytes,
        string message,
        MultiEngineExtractResponse extraction,
        string recognitionProfile)
    {
        _logger.LogWarning("Hybrid verify extraction failed. error: {Error}", extraction.Error ?? "unknown_error");

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
                    error = "hybrid_extract_failed",
                    detail = extraction.Error ?? "unknown_error",
                    recognitionProfile
                })
            };
        }

        await using var fallbackStream = new MemoryStream(probeBytes, writable: false);
        var fallbackResult = await _fallbackService.VerifyAsync(fallbackStream, recognitionProfile);
        return WrapFallbackVerifyResult(fallbackResult, extraction.Error);
    }

    private async Task<FaceMatchResult> TryFallbackTeacherVerifyAsync(
        byte[] probeBytes,
        string message,
        MultiEngineExtractResponse extraction,
        string recognitionProfile)
    {
        _logger.LogWarning("Hybrid teacher verify extraction failed. error: {Error}", extraction.Error ?? "unknown_error");

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
                    error = "hybrid_extract_failed",
                    detail = extraction.Error ?? "unknown_error",
                    recognitionProfile
                })
            };
        }

        await using var fallbackStream = new MemoryStream(probeBytes, writable: false);
        var fallbackResult = await _fallbackService.VerifyTeacherAsync(fallbackStream, recognitionProfile);
        return WrapFallbackVerifyResult(fallbackResult, extraction.Error);
    }

    private async Task<FaceMatchResult> TryFallbackVerifyByReasonAsync(
        byte[] probeBytes,
        string message,
        string reason,
        string recognitionProfile)
    {
        _logger.LogWarning("Hybrid verify cannot continue. reason: {Reason}", reason);

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
                    error = reason,
                    recognitionProfile
                })
            };
        }

        await using var fallbackStream = new MemoryStream(probeBytes, writable: false);
        var fallbackResult = await _fallbackService.VerifyAsync(fallbackStream, recognitionProfile);
        return WrapFallbackVerifyResult(fallbackResult, reason);
    }

    private async Task<FaceMatchResult> TryFallbackTeacherVerifyByReasonAsync(
        byte[] probeBytes,
        string message,
        string reason,
        string recognitionProfile)
    {
        _logger.LogWarning("Hybrid teacher verify cannot continue. reason: {Reason}", reason);

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
                    error = reason,
                    recognitionProfile
                })
            };
        }

        await using var fallbackStream = new MemoryStream(probeBytes, writable: false);
        var fallbackResult = await _fallbackService.VerifyTeacherAsync(fallbackStream, recognitionProfile);
        return WrapFallbackVerifyResult(fallbackResult, reason);
    }

    private FaceMatchResult WrapFallbackVerifyResult(FaceMatchResult fallbackResult, string? primaryError)
    {
        var fallbackMessage = $"{fallbackResult.Message} (สลับไปใช้โหมดสำรองอัตโนมัติจาก Hybrid)";
        return new FaceMatchResult
        {
            Success = fallbackResult.Success,
            StudentId = fallbackResult.StudentId,
            StudentCode = fallbackResult.StudentCode,
            StudentName = fallbackResult.StudentName,
            UserId = fallbackResult.UserId,
            UserName = fallbackResult.UserName,
            FullName = fallbackResult.FullName,
            ConfidenceScore = fallbackResult.ConfidenceScore,
            Provider = fallbackResult.Provider,
            Message = fallbackMessage,
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

    private static double BuildConfidence(double overallScore)
    {
        var clamped = Math.Clamp(overallScore, 0d, 1d);
        var value = 0.70d + (0.29d * clamped);
        return Math.Round(value, 4);
    }

    private static bool IsAmbiguousMatch(IReadOnlyList<CandidateMatch> matches)
    {
        if (matches.Count < 2)
        {
            return false;
        }

        var best = matches[0].Evaluation.OverallScore;
        var second = matches[1].Evaluation.OverallScore;
        return best < 0.20d && Math.Abs(best - second) < 0.03d;
    }

    private static bool IsAmbiguousTeacherMatch(IReadOnlyList<TeacherCandidateMatch> matches)
    {
        if (matches.Count < 2)
        {
            return false;
        }

        var best = matches[0].Evaluation.OverallScore;
        var second = matches[1].Evaluation.OverallScore;
        return best < 0.20d && Math.Abs(best - second) < 0.03d;
    }

    private bool IsLowQualityExtraction(MultiEngineExtractResponse extraction)
    {
        var qualityError = extraction.Error is not null &&
            (extraction.Error.Contains("low_quality", StringComparison.OrdinalIgnoreCase) ||
             extraction.Error.Contains("face_roi_low_quality", StringComparison.OrdinalIgnoreCase) ||
             extraction.Error.Contains("image_too_small", StringComparison.OrdinalIgnoreCase) ||
             extraction.Error.Contains("no_face_detected", StringComparison.OrdinalIgnoreCase) ||
             extraction.Error.Contains("invalid_face_crop", StringComparison.OrdinalIgnoreCase));

        var probe = extraction.Results?.FirstOrDefault();
        var threshold = Math.Clamp((double)_options.DlibMinFaceQualityScore, 0.05d, 0.85d);
        var lowQualityProbe = probe is null ||
            probe.FaceCount <= 0 ||
            probe.QualityScore <= 0d ||
            probe.QualityScore < threshold;

        return qualityError || lowQualityProbe;
    }

    private FaceMatchResult BuildLowQualityFaceMatchResult(
        byte[] probeBytes,
        string recognitionProfile,
        MultiEngineExtractResponse extraction,
        bool isTeacher = false)
    {
        var threshold = Math.Clamp((double)_options.DlibMinFaceQualityScore, 0.05d, 0.85d);
        var message = isTeacher ? "ใบหน้าครูไม่ชัด" : "ใบหน้าไม่ชัด";

        return new FaceMatchResult
        {
            Success = false,
            Message = message,
            Provider = ProviderName,
            ConfidenceScore = 0m,
            RawResponseJson = JsonSerializer.Serialize(new
            {
                error = "face_roi_low_quality",
                recognitionProfile,
                minQualityScore = Math.Round((decimal)threshold, 4),
                availableEngines = extraction.AvailableEngines,
                rawError = extraction.Error,
                probeSize = probeBytes.Length,
                isTeacher
            })
        };
    }
}
