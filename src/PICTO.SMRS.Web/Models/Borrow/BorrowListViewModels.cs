namespace PICTO.SMRS.Web.Models.Borrow;

public class BorrowIndexRowViewModel
{
    public int Id { get; set; }

    public string? RfNo { get; set; }

    public DateOnly SlipDate { get; set; }

    public int ItemCount { get; set; }

    public BorrowStatus Status { get; set; }

    public DateTimeOffset? IssuedAt { get; set; }
}

public class BorrowApprovalRowViewModel
{
    public int Id { get; set; }

    public string? RfNo { get; set; }

    public string BorrowerName { get; set; } = string.Empty;

    public string BorrowerDivision { get; set; } = string.Empty;

    public DateOnly SlipDate { get; set; }

    public int ItemCount { get; set; }

    public BorrowStatus Status { get; set; }

    public string? PendingReason { get; set; }

    public string? RejectionReason { get; set; }

    public DateTimeOffset? IssuedAt { get; set; }
}

public class BorrowDetailsViewModel
{
    public int Id { get; set; }

    public string? RfNo { get; set; }

    public string BorrowerName { get; set; } = string.Empty;

    public string BorrowerDivision { get; set; } = string.Empty;

    public string? Office { get; set; }

    public DateOnly SlipDate { get; set; }

    public string? SlipTime { get; set; }

    public string? TelNo { get; set; }

    public string? Remarks { get; set; }

    public BorrowStatus Status { get; set; }

    public string? PendingReason { get; set; }

    public string? RejectionReason { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public DateTimeOffset? RejectedAt { get; set; }

    public DateTimeOffset? IssuedAt { get; set; }

    public IReadOnlyList<BorrowDetailsItemViewModel> Items { get; set; } = [];
}

public class BorrowDetailsItemViewModel
{
    public string Description { get; set; } = string.Empty;

    public int Qty { get; set; }

    public string LocationVenue { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public DateOnly? BorrowDate { get; set; }

    public string? BorrowTime { get; set; }

    public DateOnly? ReturnDate { get; set; }

    public string? ReturnTime { get; set; }
}
