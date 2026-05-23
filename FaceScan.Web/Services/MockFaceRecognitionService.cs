using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Models.Enums;
using FaceScan.Web.Models.Options;
using FaceScan.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FaceScan.Web.Services;

public class MockFaceRecognitionService : IFaceRecognitionService
{
    private const string ProviderName = "MockFaceEngine";
    private const string EmbeddingVersion = "mock-phash-v2";
    private readonly ApplicationDbContext _dbContext;
    private readonly FaceRecognitionOptions _options;
    private readonly IWebHostEnvironment _environment;

    public MockFaceRecognitionService(
        ApplicationDbContext dbContext,
        IOptions<FaceRecognitionOptions> options,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _environment = environment;
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
                Message = "ไม่พบนักเรียน",
                Provider = ProviderName,
                QualityScore = 0m
            };
        }

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

        var vectors = await BuildFingerprintsFromPathsAsync(imagePaths);
        if (vectors.Count < 3)
        {
            profile.EnrollmentStatus = EnrollmentStatus.Pending;
            profile.TemplateVersion = EmbeddingVersion;
            profile.QualityNote = "ต้องมีรูปที่ใช้งานได้อย่างน้อย 3 รูป";
            profile.EmbeddingJson = null;
            await _dbContext.SaveChangesAsync();

            return new FaceEnrollResult
            {
                Success = false,
                Message = "ต้องมีรูปที่ใช้งานได้อย่างน้อย 3 รูป",
                Provider = ProviderName,
                QualityScore = vectors.Count == 0 ? 0m : (decimal)Math.Round(vectors.Average(x => x.Fingerprint.QualityScore), 4),
                RawResponseJson = JsonSerializer.Serialize(new { status = "pending", validImageCount = vectors.Count })
            };
        }

        var averageQuality = vectors.Average(x => x.Fingerprint.QualityScore);
        var embeddingPayload = new MockEmbeddingPayload
        {
            Version = EmbeddingVersion,
            TrainedAtUtc = DateTime.UtcNow,
            Vectors = vectors.Select(v => new MockVector
            {
                ImagePath = v.RelativePath,
                HashHex = ToHex(v.Fingerprint.Hash),
                Brightness = Math.Round(v.Fingerprint.Brightness, 6),
                Contrast = Math.Round(v.Fingerprint.Contrast, 6)
            }).ToList()
        };

        profile.EnrollmentStatus = EnrollmentStatus.Ready;
        profile.TemplateVersion = EmbeddingVersion;
        profile.LastTrainedAt = DateTime.UtcNow;
        profile.EmbeddingJson = JsonSerializer.Serialize(embeddingPayload);
        profile.QualityNote = $"ลงทะเบียน {vectors.Count} รูป (คุณภาพเฉลี่ย {averageQuality * 100:0.#}%)";
        await _dbContext.SaveChangesAsync();

        return new FaceEnrollResult
        {
            Success = true,
            Message = "ลงทะเบียนใบหน้าสำเร็จ",
            Provider = ProviderName,
            QualityScore = (decimal)Math.Round(averageQuality, 4),
            RawResponseJson = JsonSerializer.Serialize(new
            {
                status = "ready",
                validImageCount = vectors.Count,
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

        var vectors = await BuildFingerprintsFromPathsAsync(imagePaths);
        if (vectors.Count < 3)
        {
            profile.EnrollmentStatus = EnrollmentStatus.Pending;
            profile.TemplateVersion = EmbeddingVersion;
            profile.QualityNote = "ต้องมีรูปที่ใช้งานได้อย่างน้อย 3 รูป";
            profile.EmbeddingJson = null;
            await _dbContext.SaveChangesAsync();

            return new FaceEnrollResult
            {
                Success = false,
                Message = "ต้องมีรูปที่ใช้งานได้อย่างน้อย 3 รูป",
                Provider = ProviderName,
                QualityScore = vectors.Count == 0 ? 0m : (decimal)Math.Round(vectors.Average(x => x.Fingerprint.QualityScore), 4),
                RawResponseJson = JsonSerializer.Serialize(new { status = "pending", validImageCount = vectors.Count })
            };
        }

        var averageQuality = vectors.Average(x => x.Fingerprint.QualityScore);
        var embeddingPayload = new MockEmbeddingPayload
        {
            Version = EmbeddingVersion,
            TrainedAtUtc = DateTime.UtcNow,
            Vectors = vectors.Select(v => new MockVector
            {
                ImagePath = v.RelativePath,
                HashHex = ToHex(v.Fingerprint.Hash),
                Brightness = Math.Round(v.Fingerprint.Brightness, 6),
                Contrast = Math.Round(v.Fingerprint.Contrast, 6)
            }).ToList()
        };

        profile.EnrollmentStatus = EnrollmentStatus.Ready;
        profile.TemplateVersion = EmbeddingVersion;
        profile.LastTrainedAt = DateTime.UtcNow;
        profile.EmbeddingJson = JsonSerializer.Serialize(embeddingPayload);
        profile.QualityNote = $"ลงทะเบียน {vectors.Count} รูป (คุณภาพเฉลี่ย {averageQuality * 100:0.#}%)";
        await _dbContext.SaveChangesAsync();

        return new FaceEnrollResult
        {
            Success = true,
            Message = "ลงทะเบียนใบหน้าครูสำเร็จ",
            Provider = ProviderName,
            QualityScore = (decimal)Math.Round(averageQuality, 4),
            RawResponseJson = JsonSerializer.Serialize(new
            {
                status = "ready",
                validImageCount = vectors.Count,
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

        if (rawBytes.Length < 2000)
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

        var (mockCodeHint, imageBytes) = SplitMockHint(rawBytes);
        if (imageBytes.Length < 1500)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = "ไม่พบภาพสำหรับตรวจสอบ",
                Provider = ProviderName,
                ConfidenceScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new { error = "invalid_payload" })
            };
        }

        var probe = await TryBuildFingerprintAsync(imageBytes);
        if (probe is null)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = "ใบหน้าไม่ชัด",
                Provider = ProviderName,
                ConfidenceScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new { error = "decode_failed" })
            };
        }

        var candidates = await _dbContext.Students
            .Include(x => x.FaceProfile)
            .Include(x => x.StudentPhotos)
            .Where(x => x.IsActive &&
                        x.FaceProfile != null &&
                        x.FaceProfile.EnrollmentStatus == EnrollmentStatus.Ready)
            .AsNoTracking()
            .ToListAsync();

        if (candidates.Count == 0)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = "ไม่พบข้อมูลลงทะเบียนใบหน้า",
                Provider = ProviderName,
                ConfidenceScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new { error = "no_profile" })
            };
        }

        if (!string.IsNullOrWhiteSpace(mockCodeHint))
        {
            var hinted = candidates.FirstOrDefault(x => x.StudentCode.Equals(mockCodeHint, StringComparison.OrdinalIgnoreCase));
            if (hinted is not null)
            {
                return new FaceMatchResult
                {
                    Success = true,
                    StudentId = hinted.Id,
                    StudentCode = hinted.StudentCode,
                    StudentName = hinted.FullName,
                    ConfidenceScore = 0.99m,
                    Provider = ProviderName,
                    Message = "พบตัวตน",
                    RawResponseJson = JsonSerializer.Serialize(new
                    {
                        match = true,
                        mode = "mock-code-hint",
                        studentCode = hinted.StudentCode,
                        confidence = 0.99m
                    })
                };
            }
        }

        var scores = new List<CandidateScore>();
        foreach (var candidate in candidates)
        {
            var candidateVectors = await ResolveCandidateVectorsAsync(candidate);
            if (candidateVectors.Count == 0)
            {
                continue;
            }

            var bestSimilarity = candidateVectors
                .Select(x => CalculateSimilarity(probe.Value, x))
                .OrderByDescending(x => x)
                .First();

            scores.Add(new CandidateScore(candidate, bestSimilarity));
        }

        if (scores.Count == 0)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = "ไม่พบข้อมูลใบหน้าที่พร้อมใช้งาน",
                Provider = ProviderName,
                ConfidenceScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new { error = "no_valid_vector" })
            };
        }

        var bestMatch = scores.OrderByDescending(x => x.Similarity).First();
        var threshold = Math.Clamp((double)_options.MockSimilarityThreshold, 0.35d, 0.95d);
        var confidence = BuildConfidence(bestMatch.Similarity);

        if (bestMatch.Similarity < threshold)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = "ไม่พบข้อมูล",
                Provider = ProviderName,
                ConfidenceScore = confidence,
                RawResponseJson = JsonSerializer.Serialize(new
                {
                    error = "below_threshold",
                    similarity = Math.Round(bestMatch.Similarity, 4),
                    threshold = Math.Round(threshold, 4),
                    confidence
                })
            };
        }

        return new FaceMatchResult
        {
            Success = true,
            StudentId = bestMatch.Student.Id,
            StudentCode = bestMatch.Student.StudentCode,
            StudentName = bestMatch.Student.FullName,
            ConfidenceScore = confidence,
            Provider = ProviderName,
            Message = "พบตัวตน",
            RawResponseJson = JsonSerializer.Serialize(new
            {
                match = true,
                studentCode = bestMatch.Student.StudentCode,
                similarity = Math.Round(bestMatch.Similarity, 4),
                confidence
            })
        };
    }

    public async Task<FaceMatchResult> VerifyTeacherAsync(Stream imageStream, string? recognitionProfile = null)
    {
        await using var rawMemory = new MemoryStream();
        await imageStream.CopyToAsync(rawMemory);
        var rawBytes = rawMemory.ToArray();

        if (rawBytes.Length < 2000)
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

        var (mockCodeHint, imageBytes) = SplitMockHint(rawBytes);
        if (imageBytes.Length < 1500)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = "ไม่พบภาพสำหรับตรวจสอบ",
                Provider = ProviderName,
                ConfidenceScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new { error = "invalid_payload" })
            };
        }

        var probe = await TryBuildFingerprintAsync(imageBytes);
        if (probe is null)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = "ใบหน้าไม่ชัด",
                Provider = ProviderName,
                ConfidenceScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new { error = "decode_failed" })
            };
        }

        var teacherRoleIds = await _dbContext.Roles
            .Where(x => x.Name != null && TeacherRoleCatalog.AttendanceRoles.Contains(x.Name))
            .Select(x => x.Id)
            .ToListAsync();

        var teacherUserIds = await _dbContext.UserRoles
            .Where(x => teacherRoleIds.Contains(x.RoleId))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync();

        var candidates = await _dbContext.Users
            .Include(x => x.TeacherFaceProfile)
            .Include(x => x.TeacherFacePhotos)
            .Where(x => x.IsActive &&
                        teacherUserIds.Contains(x.Id) &&
                        x.TeacherFaceProfile != null &&
                        x.TeacherFaceProfile.EnrollmentStatus == EnrollmentStatus.Ready)
            .AsNoTracking()
            .ToListAsync();

        if (candidates.Count == 0)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = "ไม่พบข้อมูลลงทะเบียนใบหน้าครู",
                Provider = ProviderName,
                ConfidenceScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new { error = "no_profile" })
            };
        }

        if (!string.IsNullOrWhiteSpace(mockCodeHint))
        {
            var hinted = candidates.FirstOrDefault(x => string.Equals(x.UserName, mockCodeHint, StringComparison.OrdinalIgnoreCase));
            if (hinted is not null)
            {
                return new FaceMatchResult
                {
                    Success = true,
                    UserId = hinted.Id,
                    UserName = hinted.UserName,
                    FullName = hinted.FullName,
                    StudentCode = hinted.UserName,
                    StudentName = hinted.FullName,
                    ConfidenceScore = 0.99m,
                    Provider = ProviderName,
                    Message = "พบตัวตน",
                    RawResponseJson = JsonSerializer.Serialize(new
                    {
                        match = true,
                        mode = "mock-code-hint",
                        userName = hinted.UserName,
                        confidence = 0.99m
                    })
                };
            }
        }

        var scores = new List<TeacherCandidateScore>();
        foreach (var candidate in candidates)
        {
            var candidateVectors = await ResolveTeacherCandidateVectorsAsync(candidate);
            if (candidateVectors.Count == 0)
            {
                continue;
            }

            var bestSimilarity = candidateVectors
                .Select(x => CalculateSimilarity(probe.Value, x))
                .OrderByDescending(x => x)
                .First();

            scores.Add(new TeacherCandidateScore(candidate, bestSimilarity));
        }

        if (scores.Count == 0)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = "ไม่พบข้อมูลใบหน้าครูที่พร้อมใช้งาน",
                Provider = ProviderName,
                ConfidenceScore = 0m,
                RawResponseJson = JsonSerializer.Serialize(new { error = "no_valid_vector" })
            };
        }

        var bestMatch = scores.OrderByDescending(x => x.Similarity).First();
        var threshold = Math.Clamp((double)_options.MockSimilarityThreshold, 0.35d, 0.95d);
        var confidence = BuildConfidence(bestMatch.Similarity);

        if (bestMatch.Similarity < threshold)
        {
            return new FaceMatchResult
            {
                Success = false,
                Message = "ไม่พบข้อมูล",
                Provider = ProviderName,
                ConfidenceScore = confidence,
                RawResponseJson = JsonSerializer.Serialize(new
                {
                    error = "below_threshold",
                    similarity = Math.Round(bestMatch.Similarity, 4),
                    threshold = Math.Round(threshold, 4),
                    confidence
                })
            };
        }

        return new FaceMatchResult
        {
            Success = true,
            UserId = bestMatch.User.Id,
            UserName = bestMatch.User.UserName,
            FullName = bestMatch.User.FullName,
            StudentCode = bestMatch.User.UserName,
            StudentName = bestMatch.User.FullName,
            ConfidenceScore = confidence,
            Provider = ProviderName,
            Message = "พบตัวตน",
            RawResponseJson = JsonSerializer.Serialize(new
            {
                match = true,
                userName = bestMatch.User.UserName,
                similarity = Math.Round(bestMatch.Similarity, 4),
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
        return true;
    }

    private async Task<List<EnrollmentVector>> BuildFingerprintsFromPathsAsync(IEnumerable<string> imagePaths)
    {
        var maxVectors = Math.Max(3, _options.MockMaxVectorsPerStudent);
        var vectors = new List<EnrollmentVector>();

        foreach (var relativePath in imagePaths
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Select(NormalizeRelativePath)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var fullPath = ResolvePhysicalPath(relativePath);
            if (fullPath is null || !File.Exists(fullPath))
            {
                continue;
            }

            await using var stream = File.OpenRead(fullPath);
            var fingerprint = await TryBuildFingerprintAsync(stream);
            if (fingerprint is null)
            {
                continue;
            }

            vectors.Add(new EnrollmentVector(relativePath, fingerprint.Value));
            if (vectors.Count >= maxVectors)
            {
                break;
            }
        }

        return vectors;
    }

    private async Task<List<Fingerprint>> ResolveCandidateVectorsAsync(Student student)
    {
        var vectors = ParseVectors(student.FaceProfile?.EmbeddingJson);
        if (vectors.Count > 0)
        {
            return vectors;
        }

        var fallbackPaths = student.StudentPhotos
            .OrderByDescending(x => x.IsPrimary)
            .ThenByDescending(x => x.CapturedAt)
            .Select(x => x.FilePath)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (fallbackPaths.Count == 0)
        {
            return vectors;
        }

        var generatedVectors = await BuildFingerprintsFromPathsAsync(fallbackPaths);
        return generatedVectors.Select(x => x.Fingerprint).ToList();
    }

    private async Task<List<Fingerprint>> ResolveTeacherCandidateVectorsAsync(ApplicationUser user)
    {
        var vectors = ParseVectors(user.TeacherFaceProfile?.EmbeddingJson);
        if (vectors.Count > 0)
        {
            return vectors;
        }

        var fallbackPaths = user.TeacherFacePhotos
            .OrderByDescending(x => x.IsPrimary)
            .ThenByDescending(x => x.CapturedAt)
            .Select(x => x.FilePath)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (fallbackPaths.Count == 0)
        {
            return vectors;
        }

        var generatedVectors = await BuildFingerprintsFromPathsAsync(fallbackPaths);
        return generatedVectors.Select(x => x.Fingerprint).ToList();
    }

    private static List<Fingerprint> ParseVectors(string? embeddingJson)
    {
        if (string.IsNullOrWhiteSpace(embeddingJson))
        {
            return new List<Fingerprint>();
        }

        try
        {
            var payload = JsonSerializer.Deserialize<MockEmbeddingPayload>(embeddingJson);
            if (payload?.Vectors is null || payload.Vectors.Count == 0)
            {
                return new List<Fingerprint>();
            }

            var vectors = new List<Fingerprint>(payload.Vectors.Count);
            foreach (var vector in payload.Vectors)
            {
                if (!TryParseHex(vector.HashHex, out var hash))
                {
                    continue;
                }

                vectors.Add(new Fingerprint(
                    hash,
                    Math.Clamp(vector.Brightness, 0d, 1d),
                    Math.Clamp(vector.Contrast, 0d, 1d),
                    0.8d));
            }

            return vectors;
        }
        catch
        {
            return new List<Fingerprint>();
        }
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

    private static (string? MockCodeHint, byte[] ImageBytes) SplitMockHint(byte[] rawBytes)
    {
        var marker = Encoding.ASCII.GetBytes("MOCKCODE:");
        if (rawBytes.Length <= marker.Length + 1)
        {
            return (null, rawBytes);
        }

        if (!rawBytes.AsSpan(0, marker.Length).SequenceEqual(marker))
        {
            return (null, rawBytes);
        }

        var lineEnd = Array.IndexOf(rawBytes, (byte)'\n', marker.Length);
        if (lineEnd < 0)
        {
            return (null, rawBytes);
        }

        var codeBytes = rawBytes.AsSpan(marker.Length, lineEnd - marker.Length);
        var code = Encoding.UTF8.GetString(codeBytes).Trim();
        var imageStartIndex = lineEnd + 1;
        if (imageStartIndex >= rawBytes.Length)
        {
            return (string.IsNullOrWhiteSpace(code) ? null : code, rawBytes);
        }

        return (string.IsNullOrWhiteSpace(code) ? null : code, rawBytes[imageStartIndex..]);
    }

    private static async Task<Fingerprint?> TryBuildFingerprintAsync(byte[] imageBytes)
    {
        await using var stream = new MemoryStream(imageBytes, writable: false);
        return await TryBuildFingerprintAsync(stream);
    }

    private static async Task<Fingerprint?> TryBuildFingerprintAsync(Stream stream)
    {
        try
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using var image = await Image.LoadAsync<Rgba32>(stream);
            if (image.Width < 32 || image.Height < 32)
            {
                return null;
            }

            var cropRectangle = BuildCenterCropRectangle(image.Width, image.Height);
            using var normalized = image.Clone(ctx => ctx
                .Crop(cropRectangle)
                .Resize(9, 8, KnownResamplers.Bicubic));

            var luminance = new double[9, 8];
            var sum = 0d;

            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 9; x++)
                {
                    var pixel = normalized[x, y];
                    var value = ((pixel.R * 0.299d) + (pixel.G * 0.587d) + (pixel.B * 0.114d)) / 255d;
                    luminance[x, y] = value;
                    sum += value;
                }
            }

            var brightness = sum / 72d;
            var variance = 0d;
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 9; x++)
                {
                    var diff = luminance[x, y] - brightness;
                    variance += diff * diff;
                }
            }

            var contrast = Math.Sqrt(variance / 72d);
            var hash = 0UL;
            var bitIndex = 0;
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    if (luminance[x, y] > luminance[x + 1, y])
                    {
                        hash |= 1UL << bitIndex;
                    }

                    bitIndex++;
                }
            }

            var qualityScore = Math.Clamp((contrast * 2.8d) + 0.25d, 0.05d, 1d);
            return new Fingerprint(hash, brightness, contrast, qualityScore);
        }
        catch
        {
            return null;
        }
    }

    private static Rectangle BuildCenterCropRectangle(int width, int height)
    {
        var cropWidth = Math.Clamp((int)Math.Round(width * 0.70d), 32, width);
        var cropHeight = Math.Clamp((int)Math.Round(height * 0.78d), 32, height);
        var x = Math.Max(0, (width - cropWidth) / 2);
        var y = Math.Max(0, (height - cropHeight) / 2);
        return new Rectangle(x, y, cropWidth, cropHeight);
    }

    private static double CalculateSimilarity(Fingerprint left, Fingerprint right)
    {
        var hammingDistance = BitOperations.PopCount(left.Hash ^ right.Hash);
        var hashSimilarity = 1d - (hammingDistance / 64d);
        var brightnessGap = Math.Abs(left.Brightness - right.Brightness);
        var contrastGap = Math.Abs(left.Contrast - right.Contrast);

        var brightnessScore = 1d - Math.Min(1d, brightnessGap * 2.4d);
        var contrastScore = 1d - Math.Min(1d, contrastGap * 3.2d);

        var similarity = (hashSimilarity * 0.82d) + (brightnessScore * 0.12d) + (contrastScore * 0.06d);
        return Math.Clamp(similarity, 0d, 1d);
    }

    private decimal BuildConfidence(double similarity)
    {
        var confidenceFloor = Math.Max(0.70d, (double)_options.MockDefaultConfidence - 0.18d);
        var confidenceCeiling = 0.99d;
        var value = confidenceFloor + ((confidenceCeiling - confidenceFloor) * similarity);
        return Math.Round((decimal)Math.Clamp(value, confidenceFloor, confidenceCeiling), 4);
    }

    private static string ToHex(ulong value)
    {
        return value.ToString("X16", CultureInfo.InvariantCulture);
    }

    private static bool TryParseHex(string? text, out ulong value)
    {
        return ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    private readonly record struct Fingerprint(ulong Hash, double Brightness, double Contrast, double QualityScore);
    private readonly record struct EnrollmentVector(string RelativePath, Fingerprint Fingerprint);
    private readonly record struct CandidateScore(Student Student, double Similarity);
    private readonly record struct TeacherCandidateScore(ApplicationUser User, double Similarity);

    private sealed class MockEmbeddingPayload
    {
        public string Version { get; set; } = EmbeddingVersion;
        public DateTime TrainedAtUtc { get; set; }
        public List<MockVector> Vectors { get; set; } = new();
    }

    private sealed class MockVector
    {
        public string ImagePath { get; set; } = string.Empty;
        public string HashHex { get; set; } = string.Empty;
        public double Brightness { get; set; }
        public double Contrast { get; set; }
    }
}
