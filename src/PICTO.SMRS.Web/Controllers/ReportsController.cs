using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Models.Borrow;
using PICTO.SMRS.Web.Models.Reports;
using PICTO.SMRS.Web.Models.Requisitions;
using PICTO.SMRS.Web.Security;

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

        var pendingReqs = await requisitions.CountAsync(r => r.Status == RequisitionStatus.Pending);
        var reqsInRange = await requisitions.CountAsync(r => r.Date >= start && r.Date <= end);
        var itReqsInRange = await requisitions.CountAsync(r =>
            r.Date >= start && r.Date <= end && r.ItemType == RequisitionItemType.ItSupplies);
        var officeReqsInRange = await requisitions.CountAsync(r =>
            r.Date >= start && r.Date <= end && r.ItemType == RequisitionItemType.OfficeSupplies);
        var borrowReqsInRange = await borrowRecords.CountAsync(r => r.SlipDate >= start && r.SlipDate <= end);
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
            InventoryValue = inventoryValue,
            CostInRange = costInRange,
            CostInCurrentYear = costInCurrentYear
        };

        return View(model);
    }
}
