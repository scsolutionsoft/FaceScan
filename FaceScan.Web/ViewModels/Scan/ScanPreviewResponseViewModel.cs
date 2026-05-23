namespace FaceScan.Web.ViewModels.Scan;

public class ScanPreviewResponseViewModel
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string RecognitionProfile { get; set; } = string.Empty;
    public int? StudentId { get; set; }
    public string? StudentCode { get; set; }
    public string? StudentName { get; set; }
    public decimal Confidence { get; set; }
}
