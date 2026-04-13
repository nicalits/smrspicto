namespace PICTO.SMRS.Web.Models;

public class ManageUsersViewModel
{
    public CreateUserViewModel Form { get; set; } = new();

    public IReadOnlyList<UserAccountRowViewModel> Accounts { get; set; } = Array.Empty<UserAccountRowViewModel>();

    public string? CurrentUserId { get; set; }
}
