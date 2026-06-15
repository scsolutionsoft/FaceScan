using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.ViewModels.Teacher;

public class StudentCareHomeroomTeacherPageViewModel
{
    public StudentCareHomeroomTeacherInputViewModel Input { get; set; } = new();
    public List<StudentCareHomeroomTeacherListItemViewModel> Teachers { get; set; } = [];
    public List<StudentCareClassroomOptionViewModel> ClassroomOptions { get; set; } = [];
}

public class StudentCareHomeroomTeacherInputViewModel
{
    public string? UserId { get; set; }

    [Required]
    [StringLength(80)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(160)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(160)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(100, MinimumLength = 6)]
    public string? Password { get; set; }

    [Required]
    public int? AssignedClassroomId { get; set; }

    public bool IsActive { get; set; } = true;
}

public class StudentCareHomeroomTeacherListItemViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int? AssignedClassroomId { get; set; }
    public string AssignedClassroomName { get; set; } = "-";
    public bool IsActive { get; set; }
}
