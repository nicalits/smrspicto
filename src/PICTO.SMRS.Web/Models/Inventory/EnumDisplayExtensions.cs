using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace PICTO.SMRS.Web.Models.Inventory;

public static class EnumDisplayExtensions
{
    public static string GetDisplayName(this Enum value)
    {
        var name = value.ToString();
        if (name is null) return string.Empty;
        var fi = value.GetType().GetField(name);
        if (fi is null) return name;
        var attr = fi.GetCustomAttribute<DisplayAttribute>();
        return attr?.Name ?? name;
    }
}
