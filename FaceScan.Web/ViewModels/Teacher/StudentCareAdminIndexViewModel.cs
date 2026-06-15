namespace FaceScan.Web.ViewModels.Teacher;

public class StudentCareAdminIndexViewModel
{
    public List<BehaviorScoreCategoryViewModel> BehaviorCategories { get; set; } = [];
    public List<GoodnessBankCategoryViewModel> GoodnessCategories { get; set; } = [];
    public BehaviorScoreCategoryInputViewModel BehaviorInput { get; set; } = new();
    public GoodnessBankCategoryInputViewModel GoodnessInput { get; set; } = new();
}

public class BehaviorScoreCategoryViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ScoreChange { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public class GoodnessBankCategoryViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Point { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}
