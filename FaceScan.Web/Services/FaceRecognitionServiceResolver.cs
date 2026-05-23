using FaceScan.Web.Models;
using FaceScan.Web.Models.Options;
using FaceScan.Web.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace FaceScan.Web.Services;

public class FaceRecognitionServiceResolver : IFaceRecognitionServiceResolver
{
    private readonly HybridFaceRecognitionService _hybridFaceRecognitionService;
    private readonly MockFaceRecognitionService _mockFaceRecognitionService;
    private readonly DlibFaceRecognitionService _dlibFaceRecognitionService;
    private readonly FaceRecognitionOptions _options;

    public FaceRecognitionServiceResolver(
        HybridFaceRecognitionService hybridFaceRecognitionService,
        MockFaceRecognitionService mockFaceRecognitionService,
        DlibFaceRecognitionService dlibFaceRecognitionService,
        IOptions<FaceRecognitionOptions> options)
    {
        _hybridFaceRecognitionService = hybridFaceRecognitionService;
        _mockFaceRecognitionService = mockFaceRecognitionService;
        _dlibFaceRecognitionService = dlibFaceRecognitionService;
        _options = options.Value;
    }

    public IFaceRecognitionService GetDefaultService()
    {
        var provider = _options.Provider?.Trim();

        return provider?.ToLowerInvariant() switch
        {
            "mock" or "mockfaceengine" => _mockFaceRecognitionService,
            "legacy-dlib" or "dlib-only" or "face_recognition_dlib" => _dlibFaceRecognitionService,
            "hybrid" or "auto" or "dlib" or "face_recognition" => _hybridFaceRecognitionService,
            _ => _hybridFaceRecognitionService
        };
    }

    public IFaceRecognitionService ResolveForScan(string? recognitionProfile)
    {
        var normalizedProfile = NormalizeProfile(recognitionProfile);

        return normalizedProfile switch
        {
            FaceRecognitionProfiles.Fast => _hybridFaceRecognitionService,
            FaceRecognitionProfiles.Auto => _hybridFaceRecognitionService,
            FaceRecognitionProfiles.Balanced => _hybridFaceRecognitionService,
            FaceRecognitionProfiles.Stable => _hybridFaceRecognitionService,
            FaceRecognitionProfiles.Accurate => _hybridFaceRecognitionService,
            _ => GetDefaultService()
        };
    }

    public string NormalizeProfile(string? recognitionProfile)
    {
        return recognitionProfile?.Trim().ToLowerInvariant() switch
        {
            FaceRecognitionProfiles.Fast or "speed" or "quick" or "mock" => FaceRecognitionProfiles.Fast,
            FaceRecognitionProfiles.Auto or "hybrid" or "smart" or "default" => FaceRecognitionProfiles.Auto,
            FaceRecognitionProfiles.Stable or "steady" or "reliable" => FaceRecognitionProfiles.Stable,
            FaceRecognitionProfiles.Accurate or "accuracy" or "precision" or "dlib" => FaceRecognitionProfiles.Accurate,
            FaceRecognitionProfiles.Balanced => FaceRecognitionProfiles.Auto,
            _ => FaceRecognitionProfiles.Auto
        };
    }
}
