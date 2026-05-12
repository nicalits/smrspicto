using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Models;
using PICTO.SMRS.Web.Models.Borrow;
using PICTO.SMRS.Web.Models.Inventory;
using PICTO.SMRS.Web.Models.Reports;
using PICTO.SMRS.Web.Models.Requisitions;
using PICTO.SMRS.Web.Security;
using PICTO.SMRS.Web.Services;

namespace PICTO.SMRS.Web.Controllers;

[Authorize(Policy = SmrsPolicies.OverviewAccess)]
public sealed class ReportsController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? startDate, DateOnly? endDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentYear = today.Year;
        var end = endDate ?? today;
        var start = startDate ?? end.AddDays(-30);
        if (start > end)
            (start, end) = (end, start);

        var inventory = db.InventoryItems.AsNoTracking();
        var requisitions = db.RequisitionRecords.AsNoTracking();
        var borrowRecords = db.BorrowRecords.AsNoTracking();

        var items = await inventory.CountAsync();
        var itemUnits = await inventory.SumAsync(i => (int?)i.Quantity) ?? 0;
        var availableUnits = Math.Max(0, itemUnits);
        var inventoryValue = await inventory.SumAsync(i => (decimal?)(i.UnitPrice * i.Quantity)) ?? 0m;

        var pendingReqs = await requisitions.CountAsync(r =>
            r.Status == RequisitionStatus.InQueue || r.Status == RequisitionStatus.Pending);
        var reqsInRange = await requisitions.CountAsync(r => r.Date >= start && r.Date <= end);
        var itReqsInRange = await requisitions.CountAsync(r =>
            r.Date >= start && r.Date <= end && r.ItemType == RequisitionItemType.ItSupplies);
        var officeReqsInRange = await requisitions.CountAsync(r =>
            r.Date >= start && r.Date <= end && r.ItemType == RequisitionItemType.OfficeSupplies);
        var borrowReqsInRange = await borrowRecords.CountAsync(r => r.SlipDate >= start && r.SlipDate <= end);

        var reqsToday = await requisitions.CountAsync(r => r.Date == today);
        var borrowsToday = await borrowRecords.CountAsync(r => r.SlipDate == today);
        var totalTransactionsToday = reqsToday + borrowsToday;

        var reqRequestorIds = await requisitions
            .Where(r => r.Date >= start && r.Date <= end)
            .Select(r => r.RequestorUserId)
            .Distinct()
            .ToListAsync();
        var borrowRequestorIds = await borrowRecords
            .Where(r => r.SlipDate >= start && r.SlipDate <= end)
            .Select(r => r.BorrowerUserId)
            .Distinct()
            .ToListAsync();
        var totalEmployeeRequestors = reqRequestorIds.Union(borrowRequestorIds).Count();

        var inUse = await db.RequisitionRecordItems
            .AsNoTracking()
            .Where(i => i.RequisitionRecord != null
                && i.RequisitionRecord.Status == RequisitionStatus.Approved
                && i.RequisitionRecord.MarkedInUseAt != null)
            .SumAsync(i => (int?)i.Qty) ?? 0;
        var unitsOutForBorrowing = await db.BorrowRecordItems
            .AsNoTracking()
            .Where(i => i.BorrowRecord != null && i.BorrowRecord.Status == BorrowStatus.Approved)
            .SumAsync(i => (int?)i.Qty) ?? 0;
        var costInRange = await db.RequisitionRecordItems
            .AsNoTracking()
            .Where(i => i.RequisitionRecord != null
                && i.RequisitionRecord.Date >= start
                && i.RequisitionRecord.Date <= end
                && i.RequisitionRecord.Status == RequisitionStatus.Approved)
            .Join(
                db.InventoryItems.AsNoTracking(),
                reqItem => reqItem.InventoryItemId,
                inv => inv.Id,
                (reqItem, inv) => (decimal?)reqItem.Qty * inv.UnitPrice)
            .SumAsync() ?? 0m;
        var costInCurrentYear = await db.RequisitionRecordItems
            .AsNoTracking()
            .Where(i => i.RequisitionRecord != null
                && i.RequisitionRecord.Date.Year == currentYear
                && i.RequisitionRecord.Status == RequisitionStatus.Approved)
            .Join(
                db.InventoryItems.AsNoTracking(),
                reqItem => reqItem.InventoryItemId,
                inv => inv.Id,
                (reqItem, inv) => (decimal?)reqItem.Qty * inv.UnitPrice)
            .SumAsync() ?? 0m;

        var allItems = await inventory
            .Select(i => new { i.Id, i.ItemName, i.Brand, i.SupplyGroup, i.Quantity, i.LowStockLevel, i.LowStockSince, i.CreatedAt })
            .ToListAsync();
        var allItemIds = allItems.Select(i => i.Id).ToList();
        var unavailableByItemId = await InventoryAvailability.GetUnavailableQuantitiesAsync(db, allItemIds);

        var lowStockItems = allItems
            .Where(i => i.LowStockLevel > 0)
            .Select(i => new { i.Id, i.ItemName, i.Brand, i.SupplyGroup, i.LowStockLevel, i.LowStockSince, Available = Math.Max(0, i.Quantity - unavailableByItemId.GetValueOrDefault(i.Id)) })
            .Where(i => i.Available <= i.LowStockLevel)
            .OrderBy(i => i.Available)
            .Select(i => new LowStockItemViewModel
            {
                Id = i.Id,
                ItemName = i.ItemName,
                Brand = i.Brand,
                SupplyGroup = i.SupplyGroup,
                AvailableQuantity = i.Available,
                LowStockLevel = i.LowStockLevel,
                LowStockSince = i.LowStockSince
            })
            .ToList();

        var now = DateTimeOffset.UtcNow;
        var staleDays = 30;

        var lastRequisitionActivity = await db.RequisitionRecordItems
            .AsNoTracking()
            .Where(i => allItemIds.Contains(i.InventoryItemId)
                && i.RequisitionRecord != null
                && i.RequisitionRecord.Status == RequisitionStatus.Approved)
            .GroupBy(i => i.InventoryItemId)
            .Select(g => new { InventoryItemId = g.Key, LastDate = g.Max(i => i.RequisitionRecord!.CreatedAt) })
            .ToDictionaryAsync(g => g.InventoryItemId, g => g.LastDate);

        var lastBorrowActivity = await db.BorrowRecordItems
            .AsNoTracking()
            .Where(i => i.InventoryItemId.HasValue
                && allItemIds.Contains(i.InventoryItemId.Value)
                && i.BorrowRecord != null
                && i.BorrowRecord.Status == BorrowStatus.Approved)
            .GroupBy(i => i.InventoryItemId!.Value)
            .Select(g => new { InventoryItemId = g.Key, LastDate = g.Max(i => i.BorrowRecord!.CreatedAt) })
            .ToDictionaryAsync(g => g.InventoryItemId, g => g.LastDate);

        var staleItems = allItems
            .Select(i =>
            {
                var hasReq = lastRequisitionActivity.TryGetValue(i.Id, out var lastReq);
                var hasBorrow = lastBorrowActivity.TryGetValue(i.Id, out var lastBorrow);
                DateTimeOffset? lastActivity = (hasReq, hasBorrow) switch
                {
                    (true, true) => lastReq > lastBorrow ? lastReq : lastBorrow,
                    (true, false) => lastReq,
                    (false, true) => lastBorrow,
                    _ => null
                };
                var daysSince = lastActivity.HasValue
                    ? (int)(now - lastActivity.Value).TotalDays
                    : (int)(now - i.CreatedAt).TotalDays;
                return new { i.Id, i.ItemName, i.Brand, i.SupplyGroup, i.Quantity, LastActivity = lastActivity, DaysSince = daysSince };
            })
            .Where(i => i.DaysSince >= staleDays)
            .OrderByDescending(i => i.DaysSince)
            .Select(i => new StaleInventoryItemViewModel
            {
                Id = i.Id,
                ItemName = i.ItemName,
                Brand = i.Brand,
                SupplyGroup = i.SupplyGroup,
                Quantity = i.Quantity,
                LastActivityAt = i.LastActivity,
                DaysSinceActivity = i.DaysSince
            })
            .ToList();

        var rsLog = await requisitions
            .Where(r => r.Date >= start && r.Date <= end)
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.CreatedAt)
            .Select(r => new RequestLogRowViewModel
            {
                RequestType = r.ItemType == RequisitionItemType.ItSupplies ? "RS (IT)" : "RS (Office)",
                ReferenceNo = r.RsNo ?? ("#" + r.Id),
                RequestorName = r.RequestorName,
                Division = r.RequestorDivision,
                Date = r.Date,
                Status = r.Status.ToString(),
                ItemCount = r.Items.Count
            })
            .ToListAsync();

        var borrowLog = await borrowRecords
            .Where(r => r.SlipDate >= start && r.SlipDate <= end)
            .OrderByDescending(r => r.SlipDate)
            .ThenByDescending(r => r.CreatedAt)
            .Select(r => new RequestLogRowViewModel
            {
                RequestType = "Borrow",
                ReferenceNo = r.RfNo ?? ("#" + r.Id),
                RequestorName = r.BorrowerName,
                Division = r.BorrowerDivision,
                Date = r.SlipDate,
                Status = r.Status.ToString(),
                ItemCount = r.Items.Count
            })
            .ToListAsync();

        var requestLog = rsLog.Concat(borrowLog)
            .OrderByDescending(r => r.Date)
            .ToList();

        var model = new ReportsIndexViewModel
        {
            CurrentYear = currentYear,
            StartDate = start,
            EndDate = end,
            Items = items,
            ItemUnits = itemUnits,
            AvailableUnits = availableUnits,
            InUseUnits = inUse,
            UnitsOutForBorrowing = unitsOutForBorrowing,
            PendingRequisitions = pendingReqs,
            RequisitionsInRange = reqsInRange,
            ItRequestsInRange = itReqsInRange,
            OfficeRequestsInRange = officeReqsInRange,
            BorrowRequestsInRange = borrowReqsInRange,
            TotalTransactionsToday = totalTransactionsToday,
            TotalEmployeeRequestors = totalEmployeeRequestors,
            InventoryValue = inventoryValue,
            CostInRange = costInRange,
            CostInCurrentYear = costInCurrentYear,
            LowStockItems = lowStockItems,
            StaleItems = staleItems,
            RequestLog = requestLog
        };

        return View(model);
    }
}
