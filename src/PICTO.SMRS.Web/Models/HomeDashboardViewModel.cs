namespace PICTO.SMRS.Web.Models;

public sealed class HomeDashboardViewModel
{
    public int CheckerQueue { get; init; }

    public int ApprovalsQueue { get; init; }

    public int IssuanceQueue { get; init; }

    public int BorrowUnreturned { get; init; }

    public int SerializedUnits { get; init; }

    public int ItSuppliesEntries { get; init; }

    public int OfficeSuppliesEntries { get; init; }

    public int TotalUnits { get; init; }

    public int ReservedUnits { get; init; }

    public int AvailableUnits { get; init; }

    public int UnitsUsed { get; init; }
}
