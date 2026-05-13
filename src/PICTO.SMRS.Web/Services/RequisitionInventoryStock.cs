using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Models.Inventory;
using PICTO.SMRS.Web.Models.Requisitions;

namespace PICTO.SMRS.Web.Services;

public static class RequisitionInventoryStock
{
    public static IReadOnlyList<string> ParseSerialNumbers(string? raw) =>
        (raw ?? string.Empty)
            .Split(new[] { "\r\n", "\n", "\r", ",", ";" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

    public static async Task<string?> ValidateNewRequisitionStockAsync(
        ApplicationDbContext db,
        RequisitionItemType requisitionItemType,
        IReadOnlyList<RequisitionLineItemViewModel> lines,
        CancellationToken cancellationToken = default)
    {
        var agg = lines
            .GroupBy(l => l.InventoryItemId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

        var duplicateSerial = lines
            .SelectMany(l => ParseSerialNumbers(l.SerialNo).Select(sn => (l.InventoryItemId, Serial: sn)))
            .GroupBy(x => x)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateSerial is not null)
            return "The same serial number cannot appear twice on one requisition.";

        var ids = agg.Keys.ToList();
        var invItems = await db.InventoryItems
            .Where(i => ids.Contains(i.Id))
            .ToListAsync(cancellationToken);

        if (invItems.Count != ids.Count)
            return "One or more selected inventory items could not be found.";

        var unavailableByItemId = await InventoryAvailability.GetUnavailableQuantitiesAsync(db, ids, cancellationToken);

        foreach (var inv in invItems)
        {
            if (!SupplyGroupMatches(requisitionItemType, inv.SupplyGroup))
                return $"Item \"{inv.ItemName}\" does not match the requisition type (IT vs office).";

            var need = agg[inv.Id];
            var available = Math.Max(0, inv.Quantity - unavailableByItemId.GetValueOrDefault(inv.Id));
            if (need > available)
                return $"Not enough available stock for \"{inv.ItemName}\" (available {available}, requested {need}).";
        }

        foreach (var line in lines)
        {
            var inv = invItems.Single(i => i.Id == line.InventoryItemId);
            if (inv.IsSerialized)
            {
                var requestedSerials = ParseSerialNumbers(line.SerialNo);
                if (requestedSerials.Count != line.Qty)
                    return $"Serialized item \"{inv.ItemName}\" requires {line.Qty} serial number(s).";

                var unavailableSerials = await db.RequisitionRecordItems
                    .AsNoTracking()
                    .Where(i => i.InventoryItemId == inv.Id
                        && i.SerialNo != null
                        && i.RequisitionRecord != null
                        && (i.RequisitionRecord.Status == RequisitionStatus.InQueue
                            || i.RequisitionRecord.Status == RequisitionStatus.Pending
                            || i.RequisitionRecord.Status == RequisitionStatus.Approved))
                    .Select(i => i.SerialNo!)
                    .ToListAsync(cancellationToken);
                var unavailableSet = unavailableSerials
                    .SelectMany(ParseSerialNumbers)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var sn in requestedSerials)
                {
                    var serialExists = await db.InventoryItemSerials
                        .AnyAsync(s => s.InventoryItemId == inv.Id && s.SerialNumber == sn, cancellationToken);
                    if (!serialExists)
                        return $"Serial \"{sn}\" is not in stock for \"{inv.ItemName}\".";

                    if (unavailableSet.Contains(sn))
                        return $"Serial \"{sn}\" is already on another active requisition.";
                }
            }
        }

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
        var unavailableByItemId = await InventoryAvailability.GetUnavailableQuantitiesAsync(
            db,
            itemIds,
            cancellationToken,
            excludeRequisitionRecordId: requisition.Id);
        var requestedByItemId = requisition.Items
            .GroupBy(i => i.InventoryItemId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));

        foreach (var (itemId, requestedQty) in requestedByItemId)
        {
            if (!invById.TryGetValue(itemId, out var inv))
                return "An inventory item on this requisition no longer exists.";

            var available = Math.Max(0, inv.Quantity - unavailableByItemId.GetValueOrDefault(inv.Id));
            if (requestedQty > available)
                return $"Insufficient stock to finalize \"{inv.ItemName}\" (available {available}, line requests {requestedQty}).";
        }

        foreach (var line in requisition.Items)
        {
            var inv = invById[line.InventoryItemId];

            if (inv.IsSerialized)
            {
                var requestedSerials = ParseSerialNumbers(line.SerialNo);
                if (requestedSerials.Count != line.Qty)
                    return $"Serialized item \"{inv.ItemName}\" requires {line.Qty} serial number(s) to finalize.";

                var unavailableSerials = await db.RequisitionRecordItems
                    .AsNoTracking()
                    .Where(i => i.InventoryItemId == inv.Id
                        && i.SerialNo != null
                        && i.RequisitionRecord != null
                        && i.RequisitionRecord.Status == RequisitionStatus.Approved
                        && i.RequisitionRecord.MarkedInUseAt != null)
                    .Select(i => i.SerialNo!)
                    .ToListAsync(cancellationToken);
                var unavailableSet = unavailableSerials
                    .SelectMany(ParseSerialNumbers)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var sn in requestedSerials)
                {
                    var serialEntity = await db.InventoryItemSerials
                        .FirstOrDefaultAsync(
                            s => s.InventoryItemId == inv.Id && s.SerialNumber == sn,
                            cancellationToken);
                    if (serialEntity is null)
                        return $"Serial \"{sn}\" was not found for \"{inv.ItemName}\".";

                    if (unavailableSet.Contains(sn))
                        return $"Serial \"{sn}\" is already issued for \"{inv.ItemName}\".";
                }
            }
        }

        return null;
    }

    public static async Task<string?> RestoreFulfilledApprovalAsync(
        ApplicationDbContext db,
        RequisitionRecord requisition,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return null;
    }

    private static bool SupplyGroupMatches(RequisitionItemType type, SupplyGroup group) =>
        (type == RequisitionItemType.ItSupplies && group == SupplyGroup.ItSupplies)
        || (type == RequisitionItemType.OfficeSupplies && group == SupplyGroup.OfficeSupplies);
}
