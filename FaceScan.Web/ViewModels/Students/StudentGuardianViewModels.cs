using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace FaceScan.Web.ViewModels.Students;

public class StudentGuardianIndexViewModel
{
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string Classroom { get; set; } = "-";
    public List<StudentGuardianListItemViewModel> Guardians { get; set; } = [];
}

public class StudentGuardianListItemViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public string? Relationship { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? PhotoPath { get; set; }
    public bool IsPrimaryContact { get; set; }
}

public class StudentGuardianFormViewModel
{
    public int? Id { get; set; }

    [Required]
    public int StudentId { get; set; }

    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string Classroom { get; set; } = "-";

    [Required(ErrorMessage = "กรุณาระบุชื่อ-สกุล")]
    [StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(20)]
    public string? NationalId { get; set; }

    [StringLength(120)]
    public string? Relationship { get; set; }

    [StringLength(40)]
    public string? PhoneNumber { get; set; }

    [StringLength(160)]
    public string? Occupation { get; set; }

    public decimal? MonthlyIncome { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    public string? CurrentPhotoPath { get; set; }
    public IFormFile? Photo { get; set; }
    public bool IsPrimaryContact { get; set; }
    public List<StudentGuardianCopySourceViewModel> CopySources { get; set; } = [];
}

public class StudentGuardianCopySourceViewModel
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public string? Relationship { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Occupation { get; set; }
    public decimal? MonthlyIncome { get; set; }
    public string? Address { get; set; }
    public string? PhotoPath { get; set; }
}
