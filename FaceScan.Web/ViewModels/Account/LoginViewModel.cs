using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.ViewModels.Account;

public class LoginViewModel
{
    [Required]
    [Display(Name = "ชื่อผู้ใช้")]
    public string Username { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "รหัสผ่าน")]
    public string? Password { get; set; }

    [Display(Name = "จดจำการเข้าสู่ระบบ")]
    public bool RememberMe { get; set; }
}
