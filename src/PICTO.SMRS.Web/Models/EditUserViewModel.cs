using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PICTO.SMRS.Web.Security;

namespace PICTO.SMRS.Web.Models;

public class EditUserViewModel : IValidatableObject
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Username")]
    [StringLength(256, MinimumLength = 2)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    [Display(Name = "Employee name")]
    public string EmployeeName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    [Display(Name = "Position")]
    public string Position { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    [Display(Name = "Division")]
    public string Division { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Role")]
    public string Role { get; set; } = SmrsRoles.Employee;

    [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters.", MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    public string? ConfirmNewPassword { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(NewPassword))
            yield break;

        if (!string.Equals(NewPassword, ConfirmNewPassword, StringComparison.Ordinal))
            yield return new ValidationResult(
                "New password and confirmation do not match.",
                [nameof(ConfirmNewPassword)]);
    }

    public IEnumerable<SelectListItem> RoleOptions { get; set; } =
        SmrsRoles.All.Select(r => new SelectListItem { Value = r, Text = FormatRoleLabel(r) });

    private static string FormatRoleLabel(string role) => role switch
    {
        SmrsRoles.DepartmentHead => "Department Head",
        SmrsRoles.ItDivisionHead => "IT Division Head",
        SmrsRoles.OfficeDivisionHead => "Office Division Head",
        SmrsRoles.Encoder => "Encoder",
        SmrsRoles.Employee => "Employee",
        _ => role
    };
}
