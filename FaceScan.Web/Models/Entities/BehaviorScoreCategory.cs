namespace FaceScan.Web.Models.Entities;

public class BehaviorScoreCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int ScoreChange { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
