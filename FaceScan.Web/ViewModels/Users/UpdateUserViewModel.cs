using System.ComponentModel.DataAnnotations;

namespace FaceScan.Web.ViewModels.Users;

public class UpdateUserViewModel
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Viewer";

    public bool IsActive { get; set; }

    public int? AssignedClassroomId { get; set; }

    [MinLength(8)]
    public string? NewPassword { get; set; }
}
