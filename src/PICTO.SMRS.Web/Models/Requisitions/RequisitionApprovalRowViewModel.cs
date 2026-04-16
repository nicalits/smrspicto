namespace PICTO.SMRS.Web.Models.Requisitions;

public class RequisitionApprovalRowViewModel
{
    public int Id { get; set; }

    public string? RsNo { get; set; }

    public string RequestorName { get; set; } = string.Empty;

    public string RequestorDivision { get; set; } = string.Empty;

    public RequisitionItemType ItemType { get; set; }

    public int ItemCount { get; set; }

    public DateOnly Date { get; set; }

    public RequisitionStatus Status { get; set; }

    public DateTimeOffset? MarkedInUseAt { get; set; }
}
