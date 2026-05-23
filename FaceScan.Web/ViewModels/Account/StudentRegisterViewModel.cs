using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.ViewModels.Account;

public class StudentRegisterViewModel
{
    [Required]
    [Display(Name = "รหัสนักเรียน")]
    public string StudentCode { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "วันเดือนปีเกิด")]
    public DateTime BirthDate { get; set; }

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
}
