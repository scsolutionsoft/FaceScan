using System.Globalization;
using System.Numerics;

namespace FaceScan.Web.Services;

public sealed partial class HybridFaceRecognitionService
{
    private static Dictionary<string, ProbeFeature> BuildProbeFeatures(MultiEngineExtractResponse extraction)
    {
        var result = new Dictionary<string, ProbeFeature>(StringComparer.OrdinalIgnoreCase);
        var probe = extraction.Results?.FirstOrDefault();
        if (probe?.Engines is null)
        {
            return result;
        }

        foreach (var entry in probe.Engines)
        {
            var engineName = entry.Key;
            var value = entry.Value;
            if (value is null || !string.IsNullOrWhiteSpace(value.Error))
            {
                continue;
            }

            if (string.Equals(value.Kind, "vector", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Vector is null)
                {
                    continue;
                }

                var vector = value.Vector.ToArray();
                if (!IsValidVector(vector))
                {
                    continue;
                }

                result[engineName] = new ProbeFeature(engineName, "vector", value.Metric, vector, null);
                continue;
            }

            var hashSample = TryBuildHashSample(value.HashHex, value.Brightness, value.Contrast, value.QualityScore);
            if (hashSample is null)
            {
                continue;
            }

            result[engineName] = new ProbeFeature(engineName, "hash", value.Metric, null, hashSample);
        }

        return result;
    }

    private CandidateEvaluation? EvaluateCandidateMatch(
        IReadOnlyDictionary<string, CandidateEngineTemplate> candidateEngines,
        IReadOnlyDictionary<string, ProbeFeature> probeFeatures,
        FaceEnginePlan plan)
    {
        var matches = new List<EngineEvaluation>();

        foreach (var engineName in plan.RequestedEngines)
        {
            if (!candidateEngines.TryGetValue(engineName, out var template) ||
                !probeFeatures.TryGetValue(engineName, out var probeFeature))
            {
                continue;
            }

            var evaluation = EvaluateEngineMatch(template, probeFeature);
            if (evaluation is null || evaluation.Score <= 0d)
            {
                continue;
            }

            matches.Add(evaluation);
            if (plan.UseBestEngineOnly || matches.Count >= plan.MaxMatchedEngines)
            {
                break;
            }
        }

        if (matches.Count == 0)
        {
            return null;
        }

        var overallScore = plan.UseBestEngineOnly
            ? matches[0].Score
            : matches.Sum(x => x.Score * x.Weight) / matches.Sum(x => x.Weight);

        return new CandidateEvaluation(overallScore, matches);
    }

    private EngineEvaluation? EvaluateEngineMatch(CandidateEngineTemplate template, ProbeFeature probeFeature)
    {
        if (!string.Equals(template.Kind, probeFeature.Kind, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(template.Kind, "vector", StringComparison.OrdinalIgnoreCase))
        {
            if (template.TemplateVector is null || probeFeature.Vector is null)
            {
                return null;
            }

            if (string.Equals(template.Metric, "cosine", StringComparison.OrdinalIgnoreCase))
            {
                var similarity = CalculateCosineSimilarity(template.TemplateVector, probeFeature.Vector);
                var score = BuildNormalizedSimilarityScore(similarity, template.Threshold);
                return new EngineEvaluation(template.Engine, template.Metric, score, similarity, template.Threshold, GetEngineWeight(template.Engine));
            }

            var distance = CalculateDistance(template.TemplateVector, probeFeature.Vector);
            var scoreByDistance = BuildNormalizedDistanceScore(distance, template.Threshold);
            return new EngineEvaluation(template.Engine, template.Metric, scoreByDistance, distance, template.Threshold, GetEngineWeight(template.Engine));
        }

        if (template.HashSamples.Count == 0 || probeFeature.Hash is null)
        {
            return null;
        }

        var similarityValue = template.HashSamples
            .Select(x => CalculateHashSimilarity(probeFeature.Hash, x))
            .DefaultIfEmpty(0d)
            .Max();
        var hashScore = BuildNormalizedSimilarityScore(similarityValue, template.Threshold);
        return new EngineEvaluation(template.Engine, template.Metric, hashScore, similarityValue, template.Threshold, GetEngineWeight(template.Engine));
    }

    private static double BuildNormalizedDistanceScore(double distance, double threshold)
    {
        if (threshold <= 0d || double.IsNaN(distance) || double.IsInfinity(distance))
        {
            return -1d;
        }

        return Math.Clamp((threshold - distance) / threshold, -1d, 1d);
    }

    private static double BuildNormalizedSimilarityScore(double similarity, double threshold)
    {
        if (double.IsNaN(similarity) || double.IsInfinity(similarity))
        {
            return -1d;
        }

        var span = Math.Max(0.0001d, 1d - threshold);
        return Math.Clamp((similarity - threshold) / span, -1d, 1d);
    }

    private static double CalculateDistance(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        if (left.Count != right.Count || left.Count == 0)
        {
            return double.PositiveInfinity;
        }

        var sum = 0d;
        for (var index = 0; index < left.Count; index++)
        {
            var diff = left[index] - right[index];
            sum += diff * diff;
        }

        return Math.Sqrt(sum);
    }

    private static double CalculateCosineSimilarity(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        if (left.Count != right.Count || left.Count == 0)
        {
            return -1d;
        }

        var dot = 0d;
        var leftMagnitude = 0d;
        var rightMagnitude = 0d;

        for (var index = 0; index < left.Count; index++)
        {
            dot += left[index] * right[index];
            leftMagnitude += left[index] * left[index];
            rightMagnitude += right[index] * right[index];
        }

        if (leftMagnitude <= 0d || rightMagnitude <= 0d)
        {
            return -1d;
        }

        return dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }

    private static double CalculateHashSimilarity(HashSample probe, HashSample template)
    {
        var hammingDistance = BitOperations.PopCount(probe.Hash ^ template.Hash);
        var hashSimilarity = 1d - (hammingDistance / 64d);
        var brightnessGap = Math.Abs(probe.Brightness - template.Brightness);
        var contrastGap = Math.Abs(probe.Contrast - template.Contrast);

        var brightnessScore = 1d - Math.Min(1d, brightnessGap * 2.4d);
        var contrastScore = 1d - Math.Min(1d, contrastGap * 3.2d);

        return Math.Clamp((hashSimilarity * 0.82d) + (brightnessScore * 0.12d) + (contrastScore * 0.06d), 0d, 1d);
    }

    private static bool IsValidVector(IReadOnlyList<double>? vector)
    {
        return vector is { Count: > 0 } && vector.All(x => !double.IsNaN(x) && !double.IsInfinity(x));
    }

    private static HashSample? TryBuildHashSample(string? hashHex, double? brightness, double? contrast, double qualityScore)
    {
        if (!ulong.TryParse(hashHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hash))
        {
            return null;
        }

        return new HashSample(
            hash,
            Math.Clamp(brightness ?? 0d, 0d, 1d),
            Math.Clamp(contrast ?? 0d, 0d, 1d),
            Math.Clamp(qualityScore, 0d, 1d));
    }
}
