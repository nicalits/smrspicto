namespace PICTO.SMRS.Web.Models.Requisitions;

public enum RequisitionStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public class RequisitionRecord
{
    public int Id { get; set; }

    public string? RsNo { get; set; }

    public string RequestorUserId { get; set; } = string.Empty;

    public RequisitionItemType ItemType { get; set; }

    public DateOnly Date { get; set; }

    public string RequestorName { get; set; } = string.Empty;

    public string RequestorPosition { get; set; } = string.Empty;

    public string RequestorDivision { get; set; } = string.Empty;

    public string? Office { get; set; }

    public string? MrIcsPosition { get; set; }

    public RequisitionStatus Status { get; set; } = RequisitionStatus.Pending;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<RequisitionRecordItem> Items { get; set; } = new List<RequisitionRecordItem>();
}

public class RequisitionRecordItem
{
    public int Id { get; set; }

    public int RequisitionRecordId { get; set; }

    public RequisitionRecord? RequisitionRecord { get; set; }

    public int InventoryItemId { get; set; }

    public string? SerialNo { get; set; }

    public int Qty { get; set; }

    public string Unit { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string? RfNo { get; set; }
}
