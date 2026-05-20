using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PICTO.SMRS.Web.Validation;

/// <summary>
/// Matches <see cref="Microsoft.AspNetCore.Identity.IdentityOptions.Password"/> in Program.cs.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed partial class SmrsPasswordAttribute : ValidationAttribute, IClientModelValidator
{
    public const string ClientValidationType = "smrspassword";

    public SmrsPasswordAttribute()
    {
        ErrorMessage =
            "Password must be at least 8 characters and include uppercase, lowercase, a number, and a symbol.";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string password || string.IsNullOrEmpty(password))
            return ValidationResult.Success;

        return IsValidPassword(password)
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage, [validationContext.MemberName!]);
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var errorMessage = FormatErrorMessage(context.ModelMetadata.GetDisplayName());
        context.Attributes["data-val"] = "true";
        context.Attributes[$"data-val-{ClientValidationType}"] = errorMessage;
    }

    public static bool IsValidPassword(string password) =>
        password.Length >= 8
        && Uppercase().IsMatch(password)
        && Lowercase().IsMatch(password)
        && Digit().IsMatch(password)
        && NonAlphanumeric().IsMatch(password);

    [GeneratedRegex(@"[A-Z]")]
    private static partial Regex Uppercase();

    [GeneratedRegex(@"[a-z]")]
    private static partial Regex Lowercase();

    [GeneratedRegex(@"[0-9]")]
    private static partial Regex Digit();

    [GeneratedRegex(@"[^A-Za-z0-9]")]
    private static partial Regex NonAlphanumeric();
}
