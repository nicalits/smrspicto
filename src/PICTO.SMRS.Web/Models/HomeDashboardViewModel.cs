using PICTO.SMRS.Web.Models.Inventory;

namespace PICTO.SMRS.Web.Models;

public sealed class LowStockItemViewModel
{
    public int Id { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public string? Brand { get; init; }
    public SupplyGroup SupplyGroup { get; init; }
    public int AvailableQuantity { get; init; }
    public int LowStockLevel { get; init; }
    public DateTimeOffset? LowStockSince { get; init; }
}

public sealed class HomeDashboardViewModel
{
    public bool ShowPendingApprovals { get; init; }

    public bool ShowSplitPendingApprovals { get; init; }

    public string PendingApprovalsLabel { get; init; } = "Pending Approvals";

    public bool ShowRequisitionCheckingQueues { get; init; }

    public bool ShowBorrowDashboardInfo { get; init; }

    public bool ShowPendingBorrowRequests { get; init; }

    public int PendingCheckingQueue { get; init; }

    public int PendingApprovals { get; init; }

    public int PendingItApprovals { get; init; }

    public int PendingOfficeApprovals { get; init; }

    public int PendingIssuance { get; init; }

    public int PendingBorrowRequests { get; init; }

    public int PendingBorrowApprovals { get; init; }

    public int BorrowedItems { get; init; }

    public int ItSuppliesEntries { get; init; }

    public int OfficeSuppliesEntries { get; init; }

    public int ItAvailableUnits { get; init; }

    public int ItUnitsUsed { get; init; }

    public int ItLowStockItems { get; init; }

    public int OfficeAvailableUnits { get; init; }

    public int OfficeUnitsUsed { get; init; }

    public int OfficeLowStockItems { get; init; }

    public int TotalUnits { get; init; }

    public int AvailableUnits { get; init; }

    public int UnitsUsed { get; init; }

    public IReadOnlyList<LowStockItemViewModel> LowStockItems { get; init; } = [];
}
