using System.Globalization;
using System.Text.Json;
using FaceScan.Web.Helpers;
using FaceScan.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FaceScan.Web.Services;

public sealed partial class HybridFaceRecognitionService
{
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
            var engines = ParseCandidateEngines(row.EmbeddingJson);
            if (engines.Count == 0)
            {
                continue;
            }

            candidates.Add(new CachedStudentCandidate(
                row.StudentId,
                row.StudentCode,
                $"{row.Prefix}{row.FirstName} {row.LastName}".Trim(),
                engines));
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
            var engines = ParseCandidateEngines(row.EmbeddingJson);
            if (engines.Count == 0)
            {
                continue;
            }

            candidates.Add(new CachedTeacherCandidate(
                row.UserId,
                row.UserName,
                row.FullName,
                engines));
        }

        _memoryCache.Set(TeacherTemplateCacheKey, candidates, TemplateCacheOptions);
        return candidates;
    }

    private Dictionary<string, CandidateEngineTemplate> ParseCandidateEngines(string? embeddingJson)
    {
        if (string.IsNullOrWhiteSpace(embeddingJson))
        {
            return new Dictionary<string, CandidateEngineTemplate>(StringComparer.OrdinalIgnoreCase);
        }

        var engines = ParseHybridPayload(embeddingJson);
        if (engines.Count > 0)
        {
            return engines;
        }

        engines = ParseLegacyDlibPayload(embeddingJson);
        if (engines.Count > 0)
        {
            return engines;
        }

        return ParseLegacyMockPayload(embeddingJson);
    }

    private Dictionary<string, CandidateEngineTemplate> ParseHybridPayload(string embeddingJson)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<HybridEmbeddingPayload>(embeddingJson, JsonOptions);
            if (payload?.Engines is null || payload.Engines.Count == 0)
            {
                return new Dictionary<string, CandidateEngineTemplate>(StringComparer.OrdinalIgnoreCase);
            }

            var result = new Dictionary<string, CandidateEngineTemplate>(StringComparer.OrdinalIgnoreCase);
            foreach (var engine in payload.Engines.Where(static x => !string.IsNullOrWhiteSpace(x.Engine)))
            {
                if (TryBuildCandidateEngineTemplate(engine, out var template))
                {
                    result[template.Engine] = template;
                }
            }

            return result;
        }
        catch
        {
            return new Dictionary<string, CandidateEngineTemplate>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private Dictionary<string, CandidateEngineTemplate> ParseLegacyDlibPayload(string embeddingJson)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<LegacyDlibEmbeddingPayload>(embeddingJson, JsonOptions);
            if (payload?.TemplateVector is null || payload.TemplateVector.Count == 0)
            {
                return new Dictionary<string, CandidateEngineTemplate>(StringComparer.OrdinalIgnoreCase);
            }

            var vector = payload.TemplateVector.ToArray();
            if (!IsValidVector(vector))
            {
                return new Dictionary<string, CandidateEngineTemplate>(StringComparer.OrdinalIgnoreCase);
            }

            return new Dictionary<string, CandidateEngineTemplate>(StringComparer.OrdinalIgnoreCase)
            {
                [DlibEngine] = new CandidateEngineTemplate(
                    DlibEngine,
                    "vector",
                    "euclidean",
                    Math.Clamp((double)_options.DlibDistanceThreshold, 0.25d, 1.0d),
                    vector,
                    [])
            };
        }
        catch
        {
            return new Dictionary<string, CandidateEngineTemplate>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private Dictionary<string, CandidateEngineTemplate> ParseLegacyMockPayload(string embeddingJson)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<LegacyMockEmbeddingPayload>(embeddingJson, JsonOptions);
            if (payload?.Vectors is null || payload.Vectors.Count == 0)
            {
                return new Dictionary<string, CandidateEngineTemplate>(StringComparer.OrdinalIgnoreCase);
            }

            var samples = payload.Vectors
                .Where(static x => !string.IsNullOrWhiteSpace(x.HashHex))
                .Select(x => TryBuildHashSample(x.HashHex, x.Brightness, x.Contrast, 0.80d))
                .Where(static x => x is not null)
                .Cast<HashSample>()
                .ToList();

            if (samples.Count == 0)
            {
                return new Dictionary<string, CandidateEngineTemplate>(StringComparer.OrdinalIgnoreCase);
            }

            return new Dictionary<string, CandidateEngineTemplate>(StringComparer.OrdinalIgnoreCase)
            {
                [MockEngine] = new CandidateEngineTemplate(
                    MockEngine,
                    "hash",
                    "mock_similarity",
                    Math.Clamp((double)_options.MockSimilarityThreshold, 0.35d, 0.95d),
                    null,
                    samples)
            };
        }
        catch
        {
            return new Dictionary<string, CandidateEngineTemplate>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static bool TryBuildCandidateEngineTemplate(HybridEngineTemplatePayload payload, out CandidateEngineTemplate template)
    {
        template = default!;

        if (string.IsNullOrWhiteSpace(payload.Engine) ||
            string.IsNullOrWhiteSpace(payload.Kind) ||
            string.IsNullOrWhiteSpace(payload.Metric))
        {
            return false;
        }

        if (string.Equals(payload.Kind, "vector", StringComparison.OrdinalIgnoreCase))
        {
            if (payload.TemplateVector is null || payload.TemplateVector.Count == 0)
            {
                return false;
            }

            var vector = payload.TemplateVector.ToArray();
            if (!IsValidVector(vector))
            {
                return false;
            }

            template = new CandidateEngineTemplate(
                payload.Engine,
                "vector",
                payload.Metric,
                payload.Threshold,
                vector,
                []);
            return true;
        }

        if (payload.HashSamples is null || payload.HashSamples.Count == 0)
        {
            return false;
        }

        var samples = payload.HashSamples
            .Select(x => TryBuildHashSample(x.HashHex, x.Brightness, x.Contrast, x.QualityScore))
            .Where(static x => x is not null)
            .Cast<HashSample>()
            .ToList();

        if (samples.Count == 0)
        {
            return false;
        }

        template = new CandidateEngineTemplate(
            payload.Engine,
            "hash",
            payload.Metric,
            payload.Threshold,
            null,
            samples);
        return true;
    }
}
