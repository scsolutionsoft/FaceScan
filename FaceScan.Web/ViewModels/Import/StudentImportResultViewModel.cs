namespace FaceScan.Web.ViewModels.Import;

public class StudentImportResultViewModel
{
    public int BatchId { get; set; }
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailedRows { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = [];
}
