namespace PICTO.SMRS.Web.Models.Requisitions;

public class RequisitionIndexRowViewModel
{
    public int Id { get; set; }

    public string? RsNo { get; set; }

    public DateOnly Date { get; set; }

    public RequisitionItemType ItemType { get; set; }

    public int ItemCount { get; set; }

    public RequisitionStatus Status { get; set; }

    /// <summary>Set when encoder recorded physical issuance.</summary>
    public DateTimeOffset? MarkedInUseAt { get; set; }

    public DateTimeOffset? ReceivedAt { get; set; }
}
