using FaceScan.Web.ViewModels;

namespace FaceScan.Web.ViewModels.Users;

public class UsersIndexViewModel
{
    public IReadOnlyList<UserListItemViewModel> Users { get; set; } = [];
    public CreateUserViewModel CreateUser { get; set; } = new();
    public IReadOnlyList<SelectOptionViewModel> Classrooms { get; set; } = [];
}
