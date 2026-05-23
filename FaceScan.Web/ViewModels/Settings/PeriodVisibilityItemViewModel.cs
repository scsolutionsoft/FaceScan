namespace FaceScan.Web.ViewModels.Settings;

public class PeriodVisibilityItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public bool IsVisibleForCheck { get; set; }
    public bool MarkedForDeletion { get; set; }
}
