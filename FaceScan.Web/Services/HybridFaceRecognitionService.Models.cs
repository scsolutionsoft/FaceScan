namespace FaceScan.Web.Services;

public sealed partial class HybridFaceRecognitionService
{
    private sealed record FaceEnginePlan(IReadOnlyList<string> RequestedEngines, int MaxMatchedEngines, bool UseBestEngineOnly);
    private sealed record ProbeFeature(string Engine, string Kind, string Metric, double[]? Vector, HashSample? Hash);
    private sealed record HashSample(ulong Hash, double Brightness, double Contrast, double QualityScore);
    private sealed record CandidateEngineTemplate(string Engine, string Kind, string Metric, double Threshold, double[]? TemplateVector, IReadOnlyList<HashSample> HashSamples);
    private sealed record CachedStudentCandidate(int StudentId, string StudentCode, string StudentName, IReadOnlyDictionary<string, CandidateEngineTemplate> Engines);
    private sealed record CachedTeacherCandidate(string UserId, string UserName, string FullName, IReadOnlyDictionary<string, CandidateEngineTemplate> Engines);
    private sealed record EngineEvaluation(string Engine, string Metric, double Score, double RawValue, double Threshold, double Weight);
    private sealed record CandidateEvaluation(double OverallScore, IReadOnlyList<EngineEvaluation> MatchedEngines);
    private sealed record CandidateMatch(CachedStudentCandidate Candidate, CandidateEvaluation Evaluation);
    private sealed record TeacherCandidateMatch(CachedTeacherCandidate Candidate, CandidateEvaluation Evaluation);

    private sealed class HybridEmbeddingPayload
    {
        public string Version { get; set; } = EmbeddingVersion;
        public DateTime TrainedAtUtc { get; set; }
        public List<HybridEngineTemplatePayload> Engines { get; set; } = [];
    }

    private sealed class HybridEngineTemplatePayload
    {
        public string Engine { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Metric { get; set; } = string.Empty;
        public double Threshold { get; set; }
        public double QualityScore { get; set; }
        public int SampleCount { get; set; }
        public List<double>? TemplateVector { get; set; }
        public List<HybridHashSamplePayload>? HashSamples { get; set; }
    }

    private sealed class HybridHashSamplePayload
    {
        public string HashHex { get; set; } = string.Empty;
        public double Brightness { get; set; }
        public double Contrast { get; set; }
        public double QualityScore { get; set; }
    }

    private sealed class LegacyDlibEmbeddingPayload
    {
        public string Version { get; set; } = string.Empty;
        public List<double> TemplateVector { get; set; } = [];
    }

    private sealed class LegacyMockEmbeddingPayload
    {
        public string Version { get; set; } = string.Empty;
        public List<LegacyMockVector> Vectors { get; set; } = [];
    }

    private sealed class LegacyMockVector
    {
        public string ImagePath { get; set; } = string.Empty;
        public string HashHex { get; set; } = string.Empty;
        public double Brightness { get; set; }
        public double Contrast { get; set; }
    }
}
