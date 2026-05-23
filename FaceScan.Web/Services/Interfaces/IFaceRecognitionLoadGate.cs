namespace FaceScan.Web.Services.Interfaces;

public interface IFaceRecognitionLoadGate
{
    Task<IDisposable> EnterAsync(CancellationToken cancellationToken = default);
    int MaxConcurrency { get; }
}
