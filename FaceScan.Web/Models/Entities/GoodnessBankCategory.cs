namespace FaceScan.Web.Models.Entities;

public class GoodnessBankCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int Point { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
