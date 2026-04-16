namespace PICTO.SMRS.Web.Security;

public static class SmrsPolicies
{
    public const string UserManagement = nameof(UserManagement);
    public const string RequisitionApproval = nameof(RequisitionApproval);

    /// <summary>Approved requisitions queue for encoders and department heads (all supply types).</summary>
    public const string RequisitionChecker = nameof(RequisitionChecker);

    /// <summary>Inventory — not available to users in the <see cref="SmrsRoles.Employee"/> role (other areas use Requisitions / Borrow).</summary>
    public const string InventoryAccess = nameof(InventoryAccess);
}
