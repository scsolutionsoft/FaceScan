namespace FaceScan.Web.Services.Interfaces;

public interface IFaceRecognitionServiceResolver
{
    IFaceRecognitionService GetDefaultService();
    IFaceRecognitionService ResolveForScan(string? recognitionProfile);
    string NormalizeProfile(string? recognitionProfile);
}
