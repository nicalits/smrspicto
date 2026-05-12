using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Models.Borrow;
using PICTO.SMRS.Web.Models.Requisitions;

namespace PICTO.SMRS.Web.Services;

public static class QueuePositionHelper
{
    public static async Task<Dictionary<int, int>> GetRequisitionQueuePositionsAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        var orderedIds = await db.RequisitionRecords
            .AsNoTracking()
            .Where(r => r.Status == RequisitionStatus.InQueue)
            .OrderBy(r => r.CreatedAt)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        return orderedIds
            .Select((id, index) => (id, position: index + 1))
            .ToDictionary(x => x.id, x => x.position);
    }

    public static async Task<Dictionary<int, int>> GetBorrowQueuePositionsAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        var orderedIds = await db.BorrowRecords
            .AsNoTracking()
            .Where(r => r.Status == BorrowStatus.InQueue)
            .OrderBy(r => r.CreatedAt)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        return orderedIds
            .Select((id, index) => (id, position: index + 1))
            .ToDictionary(x => x.id, x => x.position);
    }

    public static string FormatQueueStatus(RequisitionStatus status, int? queuePosition, string? pendingReason = null)
    {
        return status switch
        {
            RequisitionStatus.InQueue when queuePosition.HasValue => $"In Que#{queuePosition.Value}",
            RequisitionStatus.InQueue => "In Queue",
            RequisitionStatus.Pending => "Pending",
            RequisitionStatus.Approved => "Approved",
            RequisitionStatus.Rejected => "Rejected",
            _ => status.ToString()
        };
    }

    public static string FormatQueueStatus(BorrowStatus status, int? queuePosition, string? pendingReason = null)
    {
        return status switch
        {
            BorrowStatus.InQueue when queuePosition.HasValue => $"In Que#{queuePosition.Value}",
            BorrowStatus.InQueue => "In Queue",
            BorrowStatus.Pending => "Pending",
            BorrowStatus.Approved => "Approved",
            BorrowStatus.Rejected => "Rejected",
            _ => status.ToString()
        };
    }
}
