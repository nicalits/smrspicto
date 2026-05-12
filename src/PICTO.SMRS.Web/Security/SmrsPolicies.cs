namespace PICTO.SMRS.Web.Security;

public static class SmrsPolicies
{
    /// <summary>Dashboard and reporting views for staff roles above Employee.</summary>
    public const string OverviewAccess = nameof(OverviewAccess);

    /// <summary>View the user list. Admin can also create/edit/delete.</summary>
    public const string UserManagement = nameof(UserManagement);

    /// <summary>View the requisition approval queue.</summary>
    public const string RequisitionApproval = nameof(RequisitionApproval);

    /// <summary>Actually approve/reject/cancel-approve a requisition (DeptHead excluded).</summary>
    public const string RequisitionApprovalAction = nameof(RequisitionApprovalAction);

    /// <summary>View the borrow approval queue.</summary>
    public const string BorrowApproval = nameof(BorrowApproval);

    /// <summary>Actually approve/reject a borrow request (DeptHead excluded).</summary>
    public const string BorrowApprovalAction = nameof(BorrowApprovalAction);

    /// <summary>View the approved-requisitions checker / issuance queue.</summary>
    public const string RequisitionChecker = nameof(RequisitionChecker);

    /// <summary>Actually mark a requisition as issued (DeptHead excluded).</summary>
    public const string RequisitionCheckerAction = nameof(RequisitionCheckerAction);

    /// <summary>View inventory stock list (all authenticated users including Employee).</summary>
    public const string InventoryAccess = nameof(InventoryAccess);

    /// <summary>Create, edit, or delete inventory items (Employee excluded).</summary>
    public const string InventoryManagement = nameof(InventoryManagement);
}
