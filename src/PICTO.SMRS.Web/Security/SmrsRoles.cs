namespace PICTO.SMRS.Web.Security;

public static class SmrsRoles
{
    public const string DepartmentHead = "DepartmentHead";
    public const string ItDivisionHead = "ItDivisionHead";
    public const string OfficeDivisionHead = "OfficeDivisionHead";
    public const string Encoder = "Encoder";
    public const string Employee = "Employee";

    public static IReadOnlyList<string> All { get; } =
    [
        DepartmentHead,
        ItDivisionHead,
        OfficeDivisionHead,
        Encoder,
        Employee
    ];

    public static IReadOnlyList<string> UserManagers { get; } =
    [
        DepartmentHead,
        ItDivisionHead,
        OfficeDivisionHead
    ];
}
