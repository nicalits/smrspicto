using PICTO.SMRS.Web.Models.Inventory;

namespace PICTO.SMRS.Web.Models.Reports;

public sealed class StaleInventoryItemViewModel
{
    public int Id { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public string? Brand { get; init; }
    public SupplyGroup SupplyGroup { get; init; }
    public int Quantity { get; init; }
    public DateTimeOffset? LastActivityAt { get; init; }
    public int DaysSinceActivity { get; init; }
}

public sealed class RequestLogRowViewModel
{
    public string RequestType { get; init; } = string.Empty;
    public string ReferenceNo { get; init; } = string.Empty;
    public string RequestorName { get; init; } = string.Empty;
    public string Division { get; init; } = string.Empty;
    public DateOnly Date { get; init; }
    public string Status { get; init; } = string.Empty;
    public int ItemCount { get; init; }
}

public sealed class ReportsIndexViewModel
{
    public int CurrentYear { get; init; }

    public DateOnly StartDate { get; init; }

    public DateOnly EndDate { get; init; }

    public int Items { get; init; }

    public int ItemUnits { get; init; }

    public int AvailableUnits { get; init; }

    public int InUseUnits { get; init; }

    public int UnitsOutForBorrowing { get; init; }

    public int PendingRequisitions { get; init; }

    public int RequisitionsInRange { get; init; }

    public int ItRequestsInRange { get; init; }

    public int OfficeRequestsInRange { get; init; }

    public int BorrowRequestsInRange { get; init; }

    public int TotalTransactionsToday { get; init; }

    public int TotalEmployeeRequestors { get; init; }

    public decimal InventoryValue { get; init; }

    public decimal CostInRange { get; init; }

    public decimal CostInCurrentYear { get; init; }

    public IReadOnlyList<LowStockItemViewModel> LowStockItems { get; init; } = [];

    public IReadOnlyList<StaleInventoryItemViewModel> StaleItems { get; init; } = [];

    public IReadOnlyList<RequestLogRowViewModel> RequestLog { get; init; } = [];
}
