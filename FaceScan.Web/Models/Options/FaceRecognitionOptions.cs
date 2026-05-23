namespace FaceScan.Web.Models.Options;

public class FaceRecognitionOptions
{
    public string Provider { get; set; } = "Hybrid";
    public decimal MockDefaultConfidence { get; set; } = 0.92m;
    public decimal MockSimilarityThreshold { get; set; } = 0.62m;
    public int MockMaxVectorsPerStudent { get; set; } = 12;

    public string DlibPythonExecutable { get; set; } = "py";
    public bool DlibAutoFallbackToMock { get; set; } = false;
    public string DlibScriptPath { get; set; } = "FaceEngine/face_recognition_dlib.py";
    public decimal DlibDistanceThreshold { get; set; } = 0.52m;
    public int DlibMinEnrollmentImages { get; set; } = 3;
    public int DlibMaxEnrollmentImages { get; set; } = 12;
    public int DlibNumJitters { get; set; } = 1;
    public int DlibUpsampleTimes { get; set; } = 1;
    public string DlibDetectionModel { get; set; } = "hog";
    public string DlibEncodingModel { get; set; } = "small";
    public decimal DlibMinFaceQualityScore { get; set; } = 0.32m;
    public int DlibProcessTimeoutSeconds { get; set; } = 25;
    public int MaxConcurrentDlibWorkers { get; set; }

    public decimal OpenCvLiteSimilarityThreshold { get; set; } = 0.72m;
    public decimal InsightFaceSimilarityThreshold { get; set; } = 0.55m;
    public decimal DeepFaceSimilarityThreshold { get; set; } = 0.48m;
}
