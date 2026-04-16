using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Models.Inventory;
using PICTO.SMRS.Web.Models.Requisitions;

namespace PICTO.SMRS.Web.Services;

public static class RequisitionInventoryReservation
{
    public static async Task<string?> ApplyReservationForNewRequisitionAsync(
        ApplicationDbContext db,
        RequisitionItemType requisitionItemType,
        IReadOnlyList<RequisitionLineItemViewModel> lines,
        CancellationToken cancellationToken = default)
    {
        var agg = lines
            .GroupBy(l => l.InventoryItemId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

        var duplicateSerial = lines
            .Where(l => !string.IsNullOrWhiteSpace(l.SerialNo))
            .GroupBy(l => (l.InventoryItemId, Serial: l.SerialNo!.Trim()))
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateSerial is not null)
            return "The same serial number cannot appear twice on one requisition.";

        var ids = agg.Keys.ToList();
        var invItems = await db.InventoryItems
            .Where(i => ids.Contains(i.Id))
            .ToListAsync(cancellationToken);

        if (invItems.Count != ids.Count)
            return "One or more selected inventory items could not be found.";

        foreach (var inv in invItems)
        {
            if (!SupplyGroupMatches(requisitionItemType, inv.SupplyGroup))
                return $"Item \"{inv.ItemName}\" does not match the requisition type (IT vs office).";

            var need = agg[inv.Id];
            var available = inv.Quantity - inv.ReservedQuantity;
            if (need > available)
                return $"Not enough available stock for \"{inv.ItemName}\" (available {available}, requested {need}).";
        }

        foreach (var line in lines)
        {
            var inv = invItems.Single(i => i.Id == line.InventoryItemId);
            if (inv.IsSerialized)
            {
                if (string.IsNullOrWhiteSpace(line.SerialNo))
                    return $"Serial number is required for \"{inv.ItemName}\".";

                var sn = line.SerialNo.Trim();
                var serialExists = await db.InventoryItemSerials
                    .AnyAsync(s => s.InventoryItemId == inv.Id && s.SerialNumber == sn, cancellationToken);
                if (!serialExists)
                    return $"Serial \"{sn}\" is not in stock for \"{inv.ItemName}\".";

                var taken = await (
                    from ri in db.RequisitionRecordItems
                    join r in db.RequisitionRecords on ri.RequisitionRecordId equals r.Id
                    where r.Status == RequisitionStatus.Pending
                          && ri.InventoryItemId == inv.Id
                          && ri.SerialNo != null
                          && ri.SerialNo == sn
                    select ri).AnyAsync(cancellationToken);
                if (taken)
                    return $"Serial \"{sn}\" is already on another pending requisition.";
            }
        }

        foreach (var inv in invItems)
            inv.ReservedQuantity += agg[inv.Id];

        return null;
    }

    public static async Task<string?> ReleaseReservationForRejectedAsync(
        ApplicationDbContext db,
        RequisitionRecord requisition,
        CancellationToken cancellationToken = default)
    {
        var agg = requisition.Items
            .GroupBy(i => i.InventoryItemId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

        var invItems = await db.InventoryItems
            .Where(i => agg.Keys.Contains(i.Id))
            .ToListAsync(cancellationToken);

        if (invItems.Count != agg.Count)
            return "Could not update inventory while releasing this reservation.";

        foreach (var inv in invItems)
            inv.ReservedQuantity = Math.Max(0, inv.ReservedQuantity - agg[inv.Id]);

        return null;
    }

    public static async Task<string?> FulfillApprovedAsync(
        ApplicationDbContext db,
        RequisitionRecord requisition,
        CancellationToken cancellationToken = default)
    {
        var itemIds = requisition.Items.Select(i => i.InventoryItemId).Distinct().ToList();
        var invById = await db.InventoryItems
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        foreach (var line in requisition.Items)
        {
            if (!invById.TryGetValue(line.InventoryItemId, out var inv))
                return "An inventory item on this requisition no longer exists.";

            if (line.Qty > inv.Quantity)
                return $"Insufficient stock to finalize \"{inv.ItemName}\" (on hand {inv.Quantity}, line requests {line.Qty}).";

            inv.ReservedQuantity = Math.Max(0, inv.ReservedQuantity - line.Qty);
            inv.Quantity -= line.Qty;

            if (inv.IsSerialized)
            {
                if (string.IsNullOrWhiteSpace(line.SerialNo))
                    return $"Serial number is required to finalize \"{inv.ItemName}\".";

                var sn = line.SerialNo.Trim();
                var serialEntity = await db.InventoryItemSerials
                    .FirstOrDefaultAsync(
                        s => s.InventoryItemId == inv.Id && s.SerialNumber == sn,
                        cancellationToken);
                if (serialEntity is null)
                    return $"Serial \"{sn}\" was not found for \"{inv.ItemName}\".";

                db.InventoryItemSerials.Remove(serialEntity);
            }
        }

        return null;
    }

    private static bool SupplyGroupMatches(RequisitionItemType type, SupplyGroup group) =>
        (type == RequisitionItemType.ItSupplies && group == SupplyGroup.ItSupplies)
        || (type == RequisitionItemType.OfficeSupplies && group == SupplyGroup.OfficeSupplies);
}
