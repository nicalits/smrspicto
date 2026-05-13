using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Models;
using PICTO.SMRS.Web.Models.Borrow;
using PICTO.SMRS.Web.Models.Inventory;
using PICTO.SMRS.Web.Models.Requisitions;
using PICTO.SMRS.Web.Security;
using PICTO.SMRS.Web.Services;

namespace PICTO.SMRS.Web.Controllers;

[Authorize(Policy = SmrsPolicies.OverviewAccess)]
public class HomeController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole(SmrsRoles.Admin);
        var isEncoder = User.IsInRole(SmrsRoles.Encoder);
        var isDepartmentHead = User.IsInRole(SmrsRoles.DepartmentHead);
        var isItDivisionHead = User.IsInRole(SmrsRoles.ItDivisionHead);
        var isOfficeDivisionHead = User.IsInRole(SmrsRoles.OfficeDivisionHead);
        var seesAll = isAdmin || isDepartmentHead;
        var isDivisionHeadOnly = !seesAll && (isItDivisionHead || isOfficeDivisionHead);
        var showPendingApprovals = !isEncoder && (seesAll || isItDivisionHead || isOfficeDivisionHead);
        var showSplitPendingApprovals = showPendingApprovals && seesAll;
        var showRequisitionCheckingQueues = isEncoder || seesAll;
        var showBorrowDashboardInfo = !isDivisionHeadOnly;
        var showPendingBorrowRequests = showBorrowDashboardInfo && !isEncoder;
        var requisitions = db.RequisitionRecords.AsNoTracking();
        var pendingApprovalRequisitions = requisitions
            .ApplyApproverQueueFilter(User)
            .Where(r => r.Status == RequisitionStatus.InQueue || r.Status == RequisitionStatus.Pending);
        var inventory = db.InventoryItems.AsNoTracking();
        var borrowedItems = db.BorrowRecordItems
            .AsNoTracking()
            .Where(i => i.BorrowRecord != null
                && i.BorrowRecord.Status == BorrowStatus.Approved);
        var inventorySnapshot = await inventory
            .Select(i => new { i.Id, i.Quantity, i.SupplyGroup, i.IsSerialized, i.LowStockLevel, i.ItemName, i.Brand, i.LowStockSince })
            .ToListAsync();
        var unavailableByItemId = await InventoryAvailability.GetUnavailableQuantitiesAsync(
            db,
            inventorySnapshot.Select(i => i.Id).ToList());
        var totalUnits = inventorySnapshot.Sum(i => i.Quantity);
        var availableUnits = inventorySnapshot.Sum(i => Math.Max(0, i.Quantity - unavailableByItemId.GetValueOrDefault(i.Id)));
        var unitsUsed = await db.RequisitionRecordItems
            .AsNoTracking()
            .Where(i => i.RequisitionRecord != null
                && i.RequisitionRecord.Status == RequisitionStatus.Approved
                && i.RequisitionRecord.MarkedInUseAt != null)
            .SumAsync(i => (int?)i.Qty) ?? 0;

        async Task<int> UnitsUsedForGroupAsync(SupplyGroup group)
        {
            var used = await db.RequisitionRecordItems
                .AsNoTracking()
                .Where(i => i.RequisitionRecord != null
                    && i.RequisitionRecord.Status == RequisitionStatus.Approved
                    && i.RequisitionRecord.MarkedInUseAt != null)
                .Join(
                    inventory.Where(i => i.SupplyGroup == group),
                    reqItem => reqItem.InventoryItemId,
                    inv => inv.Id,
                    (reqItem, _) => (int?)reqItem.Qty)
                .SumAsync();

            return used ?? 0;
        }

        var lowStockItems = inventorySnapshot
            .Where(i => i.LowStockLevel > 0)
            .Select(i => new
            {
                i.Id, i.ItemName, i.Brand, i.SupplyGroup, i.LowStockLevel, i.LowStockSince,
                Available = Math.Max(0, i.Quantity - unavailableByItemId.GetValueOrDefault(i.Id))
            })
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

        var model = new HomeDashboardViewModel
        {
            ShowPendingApprovals = showPendingApprovals,
            ShowSplitPendingApprovals = showSplitPendingApprovals,
            PendingApprovalsLabel = GetPendingApprovalsLabel(),
            ShowRequisitionCheckingQueues = showRequisitionCheckingQueues,
            ShowBorrowDashboardInfo = showBorrowDashboardInfo,
            ShowPendingBorrowRequests = showPendingBorrowRequests,
            PendingApprovals = showPendingApprovals
                ? await pendingApprovalRequisitions.CountAsync()
                : 0,
            PendingItApprovals = showSplitPendingApprovals
                ? await pendingApprovalRequisitions.CountAsync(r => r.ItemType == RequisitionItemType.ItSupplies)
                : 0,
            PendingOfficeApprovals = showSplitPendingApprovals
                ? await pendingApprovalRequisitions.CountAsync(r => r.ItemType == RequisitionItemType.OfficeSupplies)
                : 0,
            PendingCheckingQueue = showRequisitionCheckingQueues
                ? await requisitions.CountAsync(r =>
                    r.Status == RequisitionStatus.Approved && r.MarkedInUseAt == null)
                : 0,
            PendingIssuance = showRequisitionCheckingQueues
                ? await requisitions.CountAsync(r =>
                    r.Status == RequisitionStatus.Approved && r.MarkedInUseAt == null)
                : 0,
            PendingBorrowRequests = !showPendingBorrowRequests || string.IsNullOrWhiteSpace(currentUserId)
                ? 0
                : await db.BorrowRecords.CountAsync(r =>
                    r.BorrowerUserId == currentUserId
                    && (r.Status == BorrowStatus.InQueue || r.Status == BorrowStatus.Pending)),
            PendingBorrowApprovals = showBorrowDashboardInfo
                ? await db.BorrowRecords.CountAsync(r =>
                    r.Status == BorrowStatus.InQueue || r.Status == BorrowStatus.Pending)
                : 0,
            BorrowedItems = showBorrowDashboardInfo
                ? (await borrowedItems.SumAsync(i => (int?)i.Qty) ?? 0)
                : 0,
            ItSuppliesEntries = inventorySnapshot.Count(i => i.SupplyGroup == SupplyGroup.ItSupplies),
            OfficeSuppliesEntries = inventorySnapshot.Count(i => i.SupplyGroup == SupplyGroup.OfficeSupplies),
            ItAvailableUnits = inventorySnapshot
                .Where(i => i.SupplyGroup == SupplyGroup.ItSupplies)
                .Sum(i => Math.Max(0, i.Quantity - unavailableByItemId.GetValueOrDefault(i.Id))),
            ItUnitsUsed = await UnitsUsedForGroupAsync(SupplyGroup.ItSupplies),
            ItLowStockItems = lowStockItems.Count(i => i.SupplyGroup == SupplyGroup.ItSupplies),
            OfficeAvailableUnits = inventorySnapshot
                .Where(i => i.SupplyGroup == SupplyGroup.OfficeSupplies)
                .Sum(i => Math.Max(0, i.Quantity - unavailableByItemId.GetValueOrDefault(i.Id))),
            OfficeUnitsUsed = await UnitsUsedForGroupAsync(SupplyGroup.OfficeSupplies),
            OfficeLowStockItems = lowStockItems.Count(i => i.SupplyGroup == SupplyGroup.OfficeSupplies),
            TotalUnits = totalUnits,
            AvailableUnits = availableUnits,
            UnitsUsed = unitsUsed,
            LowStockItems = lowStockItems
        };

        return View(model);

        string GetPendingApprovalsLabel()
        {
            if (isItDivisionHead && !seesAll)
                return "Pending IT Approvals";
            if (isOfficeDivisionHead && !seesAll)
                return "Pending Office Approvals";
            return "Pending Approvals";
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [AllowAnonymous]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
