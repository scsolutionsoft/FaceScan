using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.ViewModels.Users;

public class CreateUserViewModel
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Viewer";

    public string FullName { get; set; } = string.Empty;
    public int? AssignedClassroomId { get; set; }
}
