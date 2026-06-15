using System.ComponentModel.DataAnnotations;
using FaceScan.Web.Models.Enums;

namespace FaceScan.Web.ViewModels.Students;

public class StudentUpsertViewModel
{
    public int? Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string StudentCode { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? NationalId { get; set; }

    [Required]
    [MaxLength(20)]
    public string Prefix { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? FirstNameEn { get; set; }

    [MaxLength(100)]
    public string? LastNameEn { get; set; }

    public GenderType Gender { get; set; } = GenderType.Unknown;
    public DateTime? BirthDate { get; set; }

    [Required]
    public int AcademicYearId { get; set; }

    [Required]
    public int GradeLevelId { get; set; }

    [Required]
    public int ClassroomId { get; set; }

    [MaxLength(20)]
    public string? RoomNumber { get; set; }

    [MaxLength(20)]
    public string? StudentNo { get; set; }

    public StudentStatus Status { get; set; } = StudentStatus.Active;

    [MaxLength(200)]
    public string? GuardianName { get; set; }

    [MaxLength(20)]
    public string? GuardianNationalId { get; set; }

    [MaxLength(20)]
    public string? GuardianPhone { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}
