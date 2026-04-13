using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PICTO.SMRS.Web.Areas.Identity.Pages.Account;

/// <summary>
/// Overrides default Identity registration: self-service signup is disabled per PRD.
/// Users are created by Department Head / Division Heads via UsersController.
/// </summary>
public class RegisterModel : PageModel
{
    public IActionResult OnGet() => RedirectToAction("Create", "Users");

    public IActionResult OnPost() => RedirectToAction("Create", "Users");
}
