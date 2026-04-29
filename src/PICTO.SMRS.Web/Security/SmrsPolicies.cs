namespace PICTO.SMRS.Web.Security;

public static class SmrsPolicies
{
    /// <summary>Dashboard and reporting views for staff roles above Employee.</summary>
    public const string OverviewAccess = nameof(OverviewAccess);

    public const string UserManagement = nameof(UserManagement);
    public const string RequisitionApproval = nameof(RequisitionApproval);
    public const string BorrowApproval = nameof(BorrowApproval);

    /// <summary>Approved requisitions queue for encoders and department heads (all supply types).</summary>
    public const string RequisitionChecker = nameof(RequisitionChecker);

    /// <summary>Inventory — not available to users in the <see cref="SmrsRoles.Employee"/> role (other areas use Requisitions / Borrow).</summary>
    public const string InventoryAccess = nameof(InventoryAccess);
}
