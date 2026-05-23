namespace FaceScan.Web.ViewModels.Students;

public class StudentPhotoViewModel
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public decimal? QualityScore { get; set; }
}
