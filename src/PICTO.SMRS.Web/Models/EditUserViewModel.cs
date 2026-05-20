using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using PICTO.SMRS.Web.Security;
using PICTO.SMRS.Web.Validation;

namespace PICTO.SMRS.Web.Models;

public class EditUserViewModel
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

    [SmrsPassword]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "New password and confirmation do not match.")]
    public string? ConfirmNewPassword { get; set; }

    public IEnumerable<SelectListItem> RoleOptions { get; set; } =
        SmrsRoles.All.Select(r => new SelectListItem { Value = r, Text = FormatRoleLabel(r) });

    private static string FormatRoleLabel(string role) => role switch
    {
        SmrsRoles.Admin => "Admin",
        SmrsRoles.DepartmentHead => "Department Head",
        SmrsRoles.ItDivisionHead => "IT Division Head",
        SmrsRoles.OfficeDivisionHead => "Office Division Head",
        SmrsRoles.Encoder => "Encoder",
        SmrsRoles.Employee => "Employee",
        _ => role
    };
}
