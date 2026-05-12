using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;

namespace PICTO.SMRS.Web.Services;

public static class LowStockTracker
{
    /// <summary>
    /// Re-evaluates low-stock status for the given items and sets or clears
    /// <c>LowStockSince</c> accordingly. Call after any stock-affecting operation.
    /// </summary>
    public static async Task RefreshAsync(
        ApplicationDbContext db,
        IReadOnlyCollection<int> itemIds,
        CancellationToken cancellationToken = default)
    {
        if (itemIds.Count == 0)
            return;

        var items = await db.InventoryItems
            .Where(i => itemIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
            return;

        var unavailable = await InventoryAvailability.GetUnavailableQuantitiesAsync(db, itemIds, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        foreach (var item in items)
        {
            if (item.LowStockLevel <= 0)
                continue;

            var available = Math.Max(0, item.Quantity - unavailable.GetValueOrDefault(item.Id));
            if (available <= item.LowStockLevel)
            {
                item.LowStockSince ??= now;
            }
            else
            {
                item.LowStockSince = null;
            }
        }
    }

    /// <summary>
    /// One-time backfill: sets <c>LowStockSince</c> to now for every item currently
    /// at or below its threshold that doesn't already have the value set.
    /// </summary>
    public static async Task BackfillAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        var candidates = await db.InventoryItems
            .Where(i => i.LowStockLevel > 0 && i.LowStockSince == null)
            .Select(i => new { i.Id, i.Quantity, i.LowStockLevel })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return;

        var unavailable = await InventoryAvailability.GetUnavailableQuantitiesAsync(
            db, candidates.Select(c => c.Id).ToList(), cancellationToken);

        var lowIds = candidates
            .Where(c => Math.Max(0, c.Quantity - unavailable.GetValueOrDefault(c.Id)) <= c.LowStockLevel)
            .Select(c => c.Id)
            .ToList();

        if (lowIds.Count == 0)
            return;

        var now = DateTimeOffset.UtcNow;
        var itemsToUpdate = await db.InventoryItems
            .Where(i => lowIds.Contains(i.Id) && i.LowStockSince == null)
            .ToListAsync(cancellationToken);

        foreach (var item in itemsToUpdate)
            item.LowStockSince = now;

        await db.SaveChangesAsync(cancellationToken);
    }
}
