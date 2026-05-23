using System.ComponentModel.DataAnnotations;
using FaceScan.Web.ViewModels;

namespace FaceScan.Web.ViewModels.Account;

public class TeacherRegisterViewModel
{
    [Required]
    [Display(Name = "ชื่อผู้ใช้")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [Display(Name = "ชื่อ-นามสกุล")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [Display(Name = "อีเมล")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "ห้องที่รับผิดชอบ")]
    public int? AssignedClassroomId { get; set; }

    [Required]
    [MinLength(8)]
    [DataType(DataType.Password)]
    [Display(Name = "รหัสผ่าน")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    [Display(Name = "ยืนยันรหัสผ่าน")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public IReadOnlyList<SelectOptionViewModel> ClassroomOptions { get; set; } = [];
}
