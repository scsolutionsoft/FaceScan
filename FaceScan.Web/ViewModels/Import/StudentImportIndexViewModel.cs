namespace FaceScan.Web.ViewModels.Import;

public class StudentImportIndexViewModel
{
    public ImportDataType ImportType { get; set; } = ImportDataType.Students;
    public string? PreviewToken { get; set; }
    public string? FileName { get; set; }
    public bool CanImport { get; set; }
    public int TotalRows { get; set; }
    public int ErrorRows { get; set; }
    public IReadOnlyList<StudentImportRowPreviewViewModel> Rows { get; set; } = [];
    public IReadOnlyList<TeacherImportRowPreviewViewModel> TeacherRows { get; set; } = [];
}
