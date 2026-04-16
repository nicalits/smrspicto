using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Models;
using PICTO.SMRS.Web.Models.Inventory;
using PICTO.SMRS.Web.Models.Requisitions;

namespace PICTO.SMRS.Web.Controllers;

public class HomeController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var requisitions = db.RequisitionRecords.AsNoTracking();
        var inventory = db.InventoryItems.AsNoTracking();
        var totalUnits = await inventory.SumAsync(i => (int?)i.Quantity) ?? 0;
        var reservedUnits = await inventory.SumAsync(i => (int?)i.ReservedQuantity) ?? 0;

        var model = new HomeDashboardViewModel
        {
            CheckerQueue = await requisitions.CountAsync(r => r.Status == RequisitionStatus.Pending),
            ApprovalsQueue = await requisitions.CountAsync(r => r.Status == RequisitionStatus.Pending),
            IssuanceQueue = await requisitions.CountAsync(r => r.Status == RequisitionStatus.Approved),
            BorrowUnreturned = 0,
            SerializedUnits = await inventory.Where(i => i.IsSerialized).SumAsync(i => i.Quantity),
            ItSuppliesEntries = await inventory.CountAsync(i => i.SupplyGroup == SupplyGroup.ItSupplies),
            OfficeSuppliesEntries = await inventory.CountAsync(i => i.SupplyGroup == SupplyGroup.OfficeSupplies),
            TotalUnits = totalUnits,
            ReservedUnits = reservedUnits,
            AvailableUnits = Math.Max(0, totalUnits - reservedUnits),
            UnitsUsed = await db.RequisitionRecords
                .AsNoTracking()
                .Where(r => r.Status == RequisitionStatus.Approved && r.MarkedInUseAt != null)
                .SelectMany(r => r.Items)
                .SumAsync(i => (int?)i.Qty) ?? 0
        };

        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [AllowAnonymous]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
