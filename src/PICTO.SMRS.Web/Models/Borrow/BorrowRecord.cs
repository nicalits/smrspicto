namespace PICTO.SMRS.Web.Models.Borrow;

public enum BorrowStatus
{
    InQueue = 1,
    Approved = 2,
    Rejected = 3,
    Pending = 4
}

public class BorrowRecord
{
    public int Id { get; set; }

    public string? RfNo { get; set; }

    public string BorrowerUserId { get; set; } = string.Empty;

    public string BorrowerName { get; set; } = string.Empty;

    public string BorrowerDivision { get; set; } = string.Empty;

    public string? Office { get; set; }

    public DateOnly SlipDate { get; set; }

    public string? SlipTime { get; set; }

    public string? TelNo { get; set; }

    public string? Remarks { get; set; }

    public BorrowStatus Status { get; set; } = BorrowStatus.InQueue;

    public string? PendingReason { get; set; }

    public string? RejectionReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public DateTimeOffset? RejectedAt { get; set; }

    /// <summary>Set at approval because borrow approval also issues the requested items.</summary>
    public DateTimeOffset? IssuedAt { get; set; }

    public string? ActionedByUserId { get; set; }

    public ICollection<BorrowRecordItem> Items { get; set; } = new List<BorrowRecordItem>();
}

public class BorrowRecordItem
{
    public int Id { get; set; }

    public int BorrowRecordId { get; set; }

    public BorrowRecord? BorrowRecord { get; set; }

    public int? InventoryItemId { get; set; }

    public string Description { get; set; } = string.Empty;

    public int Qty { get; set; }

    public string LocationVenue { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public DateOnly? BorrowDate { get; set; }

    public string? BorrowTime { get; set; }

    public DateOnly? ReturnDate { get; set; }

    public string? ReturnTime { get; set; }
}
