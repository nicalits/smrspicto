namespace PICTO.SMRS.Web.Models.Requisitions;

public class RequisitionDetailsViewModel
{
    public int Id { get; set; }

    public string? RsNo { get; set; }

    public DateOnly Date { get; set; }

    public string RequestorName { get; set; } = string.Empty;

    public string RequestorPosition { get; set; } = string.Empty;

    public string RequestorDivision { get; set; } = string.Empty;

    public string? Office { get; set; }

    public string? MrIcsPosition { get; set; }

    public RequisitionItemType ItemType { get; set; }

    public RequisitionStatus Status { get; set; }

    public string? PendingReason { get; set; }

    public string? RejectionReason { get; set; }

    public DateTimeOffset? MarkedInUseAt { get; set; }

    public DateTimeOffset? ReceivedAt { get; set; }

    public List<RequisitionDetailsItemViewModel> Items { get; set; } = [];
}

public class RequisitionDetailsItemViewModel
{
    public string ItemName { get; set; } = string.Empty;

    public string? SerialNo { get; set; }

    public int Qty { get; set; }

    public string Unit { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string? RfNo { get; set; }
}
