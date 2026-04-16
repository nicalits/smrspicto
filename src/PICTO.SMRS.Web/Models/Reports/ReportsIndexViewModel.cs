namespace PICTO.SMRS.Web.Models.Reports;

public sealed class ReportsIndexViewModel
{
    public int CurrentYear { get; init; }

    public DateOnly StartDate { get; init; }

    public DateOnly EndDate { get; init; }

    public int Items { get; init; }

    public int ItemUnits { get; init; }

    public int AvailableUnits { get; init; }

    public int InUseUnits { get; init; }

    public int PendingRequisitions { get; init; }

    public int RequisitionsInRange { get; init; }

    public decimal InventoryValue { get; init; }

    public decimal CostInRange { get; init; }

    public decimal CostInCurrentYear { get; init; }
}
