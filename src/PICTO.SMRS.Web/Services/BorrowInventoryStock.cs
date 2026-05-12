using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Models.Borrow;

namespace PICTO.SMRS.Web.Services;

public static class BorrowInventoryStock
{
    public static async Task<string?> ValidateNewBorrowStockAsync(
        ApplicationDbContext db,
        IReadOnlyList<BorrowLineItemViewModel> lines,
        CancellationToken cancellationToken = default)
    {
        var linkedItems = lines
            .Where(l => l.InventoryItemId.HasValue)
            .GroupBy(l => l.InventoryItemId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

        if (linkedItems.Count == 0)
            return null;

        var ids = linkedItems.Keys.ToList();
        var invItems = await db.InventoryItems
            .Where(i => ids.Contains(i.Id))
            .ToListAsync(cancellationToken);

        if (invItems.Count != ids.Count)
            return "One or more selected inventory items could not be found.";

        var unavailableByItemId = await InventoryAvailability.GetUnavailableQuantitiesAsync(db, ids, cancellationToken);

        foreach (var inv in invItems)
        {
            var need = linkedItems[inv.Id];
            var available = Math.Max(0, inv.Quantity - unavailableByItemId.GetValueOrDefault(inv.Id));
            if (need > available)
                return $"Not enough available stock for \"{inv.ItemName}\" (available {available}, requested {need}).";
        }

        return null;
    }
}
