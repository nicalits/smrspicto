using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Models.Borrow;
using PICTO.SMRS.Web.Models.Requisitions;

namespace PICTO.SMRS.Web.Services;

public static class InventoryAvailability
{
    public static async Task<Dictionary<int, int>> GetUnavailableQuantitiesAsync(
        ApplicationDbContext db,
        IReadOnlyCollection<int> itemIds,
        CancellationToken cancellationToken = default)
    {
        if (itemIds.Count == 0)
            return new Dictionary<int, int>();

        var requisitionQuantities = await db.RequisitionRecordItems
            .AsNoTracking()
            .Where(i => itemIds.Contains(i.InventoryItemId)
                && i.RequisitionRecord != null
                && (i.RequisitionRecord.Status == RequisitionStatus.InQueue
                    || i.RequisitionRecord.Status == RequisitionStatus.Pending
                    || (i.RequisitionRecord.Status == RequisitionStatus.Approved
                        && i.RequisitionRecord.MarkedInUseAt != null)))
            .GroupBy(i => i.InventoryItemId)
            .Select(g => new { InventoryItemId = g.Key, Quantity = g.Sum(i => i.Qty) })
            .ToListAsync(cancellationToken);

        var borrowQuantities = await db.BorrowRecordItems
            .AsNoTracking()
            .Where(i => i.InventoryItemId.HasValue
                && itemIds.Contains(i.InventoryItemId.Value)
                && i.BorrowRecord != null
                && (i.BorrowRecord.Status == BorrowStatus.InQueue
                    || i.BorrowRecord.Status == BorrowStatus.Pending
                    || i.BorrowRecord.Status == BorrowStatus.Approved))
            .GroupBy(i => i.InventoryItemId!.Value)
            .Select(g => new { InventoryItemId = g.Key, Quantity = g.Sum(i => i.Qty) })
            .ToListAsync(cancellationToken);

        var unavailableByItemId = requisitionQuantities
            .Concat(borrowQuantities)
            .GroupBy(i => i.InventoryItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        return unavailableByItemId;
    }
}
