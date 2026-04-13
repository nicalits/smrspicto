using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Models.Requisitions;
using PICTO.SMRS.Web.Models.Inventory;
using PICTO.SMRS.Web.Security;

namespace PICTO.SMRS.Web.Controllers;

[Authorize]
public class RequisitionsController : Controller
{
    private readonly ApplicationDbContext _db;

    public RequisitionsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId))
            return View(Array.Empty<RequisitionIndexRowViewModel>());

        var rows = await _db.RequisitionRecords
            .AsNoTracking()
            .Where(r => r.RequestorUserId == currentUserId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RequisitionIndexRowViewModel
            {
                Id = r.Id,
                RsNo = r.RsNo,
                Date = r.Date,
                ItemType = r.ItemType,
                ItemCount = r.Items.Count,
                Status = r.Status
            })
            .ToListAsync(cancellationToken);

        return View(rows);
    }

    [HttpGet]
    public IActionResult Create()
    {
        var vm = new RequisitionCreateViewModel
        {
            RsNo = string.Empty,
            Date = DateOnly.FromDateTime(DateTime.Today),
            RequestorName = User.FindFirstValue(SmrsClaimTypes.EmployeeName) ?? string.Empty,
            RequestorPosition = User.FindFirstValue(SmrsClaimTypes.Position) ?? string.Empty,
            RequestorDivision = User.FindFirstValue(SmrsClaimTypes.Division) ?? string.Empty,
            Items = [new RequisitionLineItemViewModel { Qty = 1 }]
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RequisitionCreateViewModel model, CancellationToken cancellationToken)
    {
        model.Date = DateOnly.FromDateTime(DateTime.Today);
        model.RequestorName = User.FindFirstValue(SmrsClaimTypes.EmployeeName) ?? model.RequestorName;
        model.RequestorPosition = User.FindFirstValue(SmrsClaimTypes.Position) ?? model.RequestorPosition;
        model.RequestorDivision = User.FindFirstValue(SmrsClaimTypes.Division) ?? model.RequestorDivision;
        model.RsNo = (model.RsNo ?? string.Empty).Trim().ToUpperInvariant();

        model.Items ??= [];
        model.Items = model.Items
            .Where(i => i is not null)
            .ToList();

        if (model.Items.Count == 0)
            ModelState.AddModelError(nameof(model.Items), "Add at least one item.");

        if (!ModelState.IsValid)
            return View(model);

        var requisition = new RequisitionRecord
        {
            RsNo = model.RsNo,
            RequestorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            ItemType = model.ItemType,
            Date = model.Date,
            RequestorName = model.RequestorName.Trim(),
            RequestorPosition = model.RequestorPosition.Trim(),
            RequestorDivision = model.RequestorDivision.Trim(),
            Office = string.IsNullOrWhiteSpace(model.Office) ? null : model.Office.Trim(),
            MrIcsPosition = string.IsNullOrWhiteSpace(model.MrIcsPosition) ? null : model.MrIcsPosition.Trim(),
            Status = RequisitionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            Items = model.Items.Select(i => new RequisitionRecordItem
            {
                InventoryItemId = i.InventoryItemId,
                SerialNo = string.IsNullOrWhiteSpace(i.SerialNo) ? null : i.SerialNo.Trim(),
                Qty = i.Qty,
                Unit = i.Unit.Trim(),
                Purpose = i.Purpose.Trim(),
                RfNo = string.IsNullOrWhiteSpace(i.RfNo) ? null : i.RfNo.Trim()
            }).ToList()
        };

        _db.RequisitionRecords.Add(requisition);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = "Requisition form submitted and sent for approval.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Policy = SmrsPolicies.RequisitionApproval)]
    public async Task<IActionResult> Approvals(CancellationToken cancellationToken)
    {
        var query = _db.RequisitionRecords
            .AsNoTracking()
            .Include(r => r.Items)
            .OrderByDescending(r => r.CreatedAt)
            .AsQueryable();

        if (User.IsInRole(SmrsRoles.ItDivisionHead) && !User.IsInRole(SmrsRoles.DepartmentHead))
            query = query.Where(r => r.ItemType == RequisitionItemType.ItSupplies);
        else if (User.IsInRole(SmrsRoles.OfficeDivisionHead) && !User.IsInRole(SmrsRoles.DepartmentHead))
            query = query.Where(r => r.ItemType == RequisitionItemType.OfficeSupplies);

        var rows = await query
            .Select(r => new RequisitionApprovalRowViewModel
            {
                Id = r.Id,
                RequestorName = r.RequestorName,
                RequestorDivision = r.RequestorDivision,
                ItemType = r.ItemType,
                ItemCount = r.Items.Count,
                Date = r.Date,
                Status = r.Status
            })
            .ToListAsync(cancellationToken);

        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var requisition = await _db.RequisitionRecords
            .AsNoTracking()
            .Include(r => r.Items)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (requisition is null)
            return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var canApprove = User.IsInRole(SmrsRoles.DepartmentHead)
            || User.IsInRole(SmrsRoles.ItDivisionHead)
            || User.IsInRole(SmrsRoles.OfficeDivisionHead);
        var canViewAsApprover = canApprove && CanApproveItemType(requisition.ItemType);
        var isRequestOwner = !string.IsNullOrWhiteSpace(currentUserId)
            && string.Equals(requisition.RequestorUserId, currentUserId, StringComparison.Ordinal);

        if (!canViewAsApprover && !isRequestOwner)
            return Forbid();

        ViewData["CanTakeAction"] = canViewAsApprover && requisition.Status == RequisitionStatus.Pending;
        ViewData["BackAction"] = canViewAsApprover ? nameof(Approvals) : nameof(Index);

        var inventoryItemIds = requisition.Items
            .Select(i => i.InventoryItemId)
            .Distinct()
            .ToList();

        var inventoryNames = await _db.InventoryItems
            .AsNoTracking()
            .Where(i => inventoryItemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.ItemName, cancellationToken);

        var vm = new RequisitionDetailsViewModel
        {
            Id = requisition.Id,
            RsNo = requisition.RsNo,
            Date = requisition.Date,
            RequestorName = requisition.RequestorName,
            RequestorPosition = requisition.RequestorPosition,
            RequestorDivision = requisition.RequestorDivision,
            Office = requisition.Office,
            MrIcsPosition = requisition.MrIcsPosition,
            ItemType = requisition.ItemType,
            Status = requisition.Status,
            Items = requisition.Items.Select(i => new RequisitionDetailsItemViewModel
            {
                ItemName = inventoryNames.GetValueOrDefault(i.InventoryItemId, $"Item #{i.InventoryItemId}"),
                SerialNo = i.SerialNo,
                Qty = i.Qty,
                Unit = i.Unit,
                Purpose = i.Purpose,
                RfNo = i.RfNo
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SmrsPolicies.RequisitionApproval)]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        var requisition = await _db.RequisitionRecords.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (requisition is null)
            return NotFound();
        if (!CanApproveItemType(requisition.ItemType))
            return Forbid();
        if (requisition.Status != RequisitionStatus.Pending)
        {
            TempData["ErrorMessage"] = "This requisition is already processed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        requisition.Status = RequisitionStatus.Approved;
        await _db.SaveChangesAsync(cancellationToken);
        TempData["StatusMessage"] = "Requisition approved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SmrsPolicies.RequisitionApproval)]
    public async Task<IActionResult> Reject(int id, CancellationToken cancellationToken)
    {
        var requisition = await _db.RequisitionRecords.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (requisition is null)
            return NotFound();
        if (!CanApproveItemType(requisition.ItemType))
            return Forbid();
        if (requisition.Status != RequisitionStatus.Pending)
        {
            TempData["ErrorMessage"] = "This requisition is already processed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        requisition.Status = RequisitionStatus.Rejected;
        await _db.SaveChangesAsync(cancellationToken);
        TempData["StatusMessage"] = "Requisition rejected.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private bool CanApproveItemType(RequisitionItemType itemType)
    {
        if (User.IsInRole(SmrsRoles.DepartmentHead))
            return true;
        if (User.IsInRole(SmrsRoles.ItDivisionHead) && itemType == RequisitionItemType.ItSupplies)
            return true;
        if (User.IsInRole(SmrsRoles.OfficeDivisionHead) && itemType == RequisitionItemType.OfficeSupplies)
            return true;
        return false;
    }

    [HttpGet]
    public async Task<IActionResult> InventoryItems(RequisitionItemType type, CancellationToken cancellationToken)
    {
        var group = type == RequisitionItemType.OfficeSupplies ? SupplyGroup.OfficeSupplies : SupplyGroup.ItSupplies;
        var items = await _db.InventoryItems
            .AsNoTracking()
            .Where(i => i.SupplyGroup == group)
            .OrderBy(i => i.ItemName)
            .Select(i => new
            {
                id = i.Id,
                name = i.ItemName,
                isSerialized = i.IsSerialized,
                unit = i.Unit.GetDisplayName()
            })
            .ToListAsync(cancellationToken);

        return Json(items);
    }

    [HttpGet]
    public async Task<IActionResult> InventoryItemSerials(int id, CancellationToken cancellationToken)
    {
        var serials = await _db.InventoryItemSerials
            .AsNoTracking()
            .Where(s => s.InventoryItemId == id)
            .OrderBy(s => s.SerialNumber)
            .Select(s => s.SerialNumber)
            .ToListAsync(cancellationToken);

        return Json(serials);
    }
}
