using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Models;
using PICTO.SMRS.Web.Security;

namespace PICTO.SMRS.Web.Controllers;

[Authorize(Policy = SmrsPolicies.UserManagement)]
public class UsersController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;

    public UsersController(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewData["CanManageUsers"] = User.IsInRole(SmrsRoles.Admin);
        return View(await BuildPageForCurrentUserAsync(new CreateUserViewModel()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = SmrsRoles.Admin)]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!SmrsRoles.All.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), "Invalid role selected.");

        if (!ModelState.IsValid)
            return View(await BuildPageForCurrentUserAsync(model));

        var user = new IdentityUser
        {
            UserName = model.UserName.Trim(),
            Email = model.Email.Trim(),
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            ApplyIdentityErrors(ModelState, result, nameof(model.Password));
            return View(await BuildPageForCurrentUserAsync(model));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            ApplyIdentityErrors(ModelState, roleResult);
            return View(await BuildPageForCurrentUserAsync(model));
        }

        var profileClaims = new[]
        {
            new Claim(SmrsClaimTypes.EmployeeName, model.EmployeeName.Trim()),
            new Claim(SmrsClaimTypes.Position, model.Position.Trim()),
            new Claim(SmrsClaimTypes.Division, model.Division.Trim())
        };
        var claimResult = await _userManager.AddClaimsAsync(user, profileClaims);
        if (!claimResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            ApplyIdentityErrors(ModelState, claimResult);
            return View(await BuildPageForCurrentUserAsync(model));
        }

        TempData["StatusMessage"] = $"User '{user.UserName}' was created with role {model.Role}.";
        return Redirect($"{Url.Action(nameof(Create))!}#accounts");
    }

    [HttpGet]
    [Authorize(Roles = SmrsRoles.Admin)]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        var vm = await MapToEditViewModelAsync(user);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = SmrsRoles.Admin)]
    public async Task<IActionResult> Edit(EditUserViewModel model)
    {
        if (!SmrsRoles.All.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), "Invalid role selected.");

        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByIdAsync(model.Id);
        if (user is null)
            return NotFound();

        var setName = await _userManager.SetUserNameAsync(user, model.UserName.Trim());
        if (!setName.Succeeded)
        {
            ApplyIdentityErrors(ModelState, setName, nameof(model.UserName));
            return View(model);
        }

        user.Email = model.Email.Trim();
        user.NormalizedEmail = _userManager.NormalizeEmail(model.Email.Trim());
        user.EmailConfirmed = true;
        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            ApplyIdentityErrors(ModelState, update, nameof(model.Email));
            return View(model);
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var removeRoles = await _userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!removeRoles.Succeeded)
        {
            ApplyIdentityErrors(ModelState, removeRoles);
            return View(model);
        }

        var addRole = await _userManager.AddToRoleAsync(user, model.Role);
        if (!addRole.Succeeded)
        {
            ApplyIdentityErrors(ModelState, addRole, nameof(model.Role));
            return View(model);
        }

        await ReplaceProfileClaimsAsync(user, model.EmployeeName.Trim(), model.Position.Trim(), model.Division.Trim());

        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var pwd = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
            if (!pwd.Succeeded)
            {
                ApplyIdentityErrors(ModelState, pwd, nameof(model.NewPassword));
                return View(model);
            }
        }

        TempData["StatusMessage"] = $"Account '{model.UserName}' was updated.";
        return Redirect($"{Url.Action(nameof(Create))!}#accounts");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = SmrsRoles.Admin)]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            TempData["ErrorMessage"] = "Invalid account.";
            return RedirectToAction(nameof(Create));
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.Equals(id, currentUserId, StringComparison.Ordinal))
        {
            TempData["ErrorMessage"] = "You cannot delete your own account.";
            return Redirect($"{Url.Action(nameof(Create))!}#accounts");
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            TempData["ErrorMessage"] = "Account was not found.";
            return Redirect($"{Url.Action(nameof(Create))!}#accounts");
        }

        var userName = user.UserName;
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return Redirect($"{Url.Action(nameof(Create))!}#accounts");
        }

        TempData["StatusMessage"] = $"User '{userName}' was removed.";
        return Redirect($"{Url.Action(nameof(Create))!}#accounts");
    }

    private async Task<EditUserViewModel> MapToEditViewModelAsync(IdentityUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var claims = await _userManager.GetClaimsAsync(user);
        return new EditUserViewModel
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            EmployeeName = claims.FirstOrDefault(c => c.Type == SmrsClaimTypes.EmployeeName)?.Value ?? string.Empty,
            Position = claims.FirstOrDefault(c => c.Type == SmrsClaimTypes.Position)?.Value ?? string.Empty,
            Division = claims.FirstOrDefault(c => c.Type == SmrsClaimTypes.Division)?.Value ?? string.Empty,
            Role = roles.FirstOrDefault() ?? SmrsRoles.Employee
        };
    }

    private async Task ReplaceProfileClaimsAsync(IdentityUser user, string employeeName, string position, string division)
    {
        var claims = await _userManager.GetClaimsAsync(user);
        foreach (var c in claims.Where(c =>
                     c.Type == SmrsClaimTypes.EmployeeName
                     || c.Type == SmrsClaimTypes.Position
                     || c.Type == SmrsClaimTypes.Division))
            await _userManager.RemoveClaimAsync(user, c);

        await _userManager.AddClaimsAsync(user,
        [
            new Claim(SmrsClaimTypes.EmployeeName, employeeName),
            new Claim(SmrsClaimTypes.Position, position),
            new Claim(SmrsClaimTypes.Division, division)
        ]);
    }

    private static void ApplyIdentityErrors(
        ModelStateDictionary modelState,
        IdentityResult result,
        string? preferredField = null)
    {
        foreach (var error in result.Errors)
        {
            var key = error.Code switch
            {
                "DuplicateUserName" or "InvalidUserName" => nameof(CreateUserViewModel.UserName),
                "DuplicateEmail" or "InvalidEmail" => nameof(CreateUserViewModel.Email),
                _ when error.Code.StartsWith("Password", StringComparison.OrdinalIgnoreCase)
                    => preferredField ?? nameof(CreateUserViewModel.Password),
                _ => preferredField ?? string.Empty
            };
            modelState.AddModelError(key, error.Description);
        }
    }

    private async Task<ManageUsersViewModel> BuildPageForCurrentUserAsync(CreateUserViewModel form)
    {
        return new ManageUsersViewModel
        {
            Form = form,
            Accounts = await LoadAccountsAsync(),
            CurrentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        };
    }

    private async Task<IReadOnlyList<UserAccountRowViewModel>> LoadAccountsAsync()
    {
        var users = await _userManager.Users
            .OrderBy(u => u.UserName)
            .ToListAsync();

        var rows = new List<UserAccountRowViewModel>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            var claims = await _userManager.GetClaimsAsync(u);
            rows.Add(new UserAccountRowViewModel
            {
                Id = u.Id,
                UserName = u.UserName ?? string.Empty,
                Email = u.Email ?? string.Empty,
                EmployeeName = claims.FirstOrDefault(c => c.Type == SmrsClaimTypes.EmployeeName)?.Value ?? "—",
                Position = claims.FirstOrDefault(c => c.Type == SmrsClaimTypes.Position)?.Value ?? "—",
                Division = claims.FirstOrDefault(c => c.Type == SmrsClaimTypes.Division)?.Value ?? "—",
                Roles = string.Join(", ", roles.OrderBy(r => r))
            });
        }

        return rows;
    }
}
