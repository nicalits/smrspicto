using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Models.Requisitions;
using PICTO.SMRS.Web.Models.Inventory;
using PICTO.SMRS.Web.Security;
using PICTO.SMRS.Web.Services;

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
                Status = r.Status,
                MarkedInUseAt = r.MarkedInUseAt
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
        ViewData["FormAction"] = nameof(Create);
        ViewData["SubmitLabel"] = "Submit";
        ViewData["PageHeading"] = "New requisition slip";
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

        foreach (var item in model.Items)
            item.RfNo = string.IsNullOrWhiteSpace(item.RfNo) ? null : item.RfNo.Trim();

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

        await using (var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
        {
            var reserveError = await RequisitionInventoryReservation.ApplyReservationForNewRequisitionAsync(
                _db, model.ItemType, model.Items, cancellationToken);
            if (reserveError is not null)
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                ModelState.AddModelError(string.Empty, reserveError);
                return View(model);
            }

            _db.RequisitionRecords.Add(requisition);
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }

        TempData["StatusMessage"] = "Requisition form submitted and sent for approval.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var requisition = await _db.RequisitionRecords
            .AsNoTracking()
            .Include(r => r.Items)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (requisition is null)
            return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId)
            || !string.Equals(requisition.RequestorUserId, currentUserId, StringComparison.Ordinal))
            return Forbid();

        if (requisition.Status != RequisitionStatus.Pending)
        {
            TempData["ErrorMessage"] = "Only pending requisitions can be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new RequisitionCreateViewModel
        {
            RsNo = requisition.RsNo ?? string.Empty,
            ItemType = requisition.ItemType,
            Date = requisition.Date,
            RequestorName = requisition.RequestorName,
            RequestorPosition = requisition.RequestorPosition,
            RequestorDivision = requisition.RequestorDivision,
            Office = requisition.Office,
            MrIcsPosition = requisition.MrIcsPosition,
            Items = requisition.Items.Select(i => new RequisitionLineItemViewModel
            {
                InventoryItemId = i.InventoryItemId,
                SerialNo = i.SerialNo,
                Qty = i.Qty,
                Unit = i.Unit,
                Purpose = i.Purpose,
                RfNo = i.RfNo
            }).ToList()
        };

        ViewData["FormAction"] = nameof(Edit);
        ViewData["EditId"] = id;
        ViewData["SubmitLabel"] = "Save changes";
        ViewData["PageHeading"] = "Edit requisition slip";
        return View(nameof(Create), model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, RequisitionCreateViewModel model, CancellationToken cancellationToken)
    {
        var requisition = await _db.RequisitionRecords
            .Include(r => r.Items)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (requisition is null)
            return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId)
            || !string.Equals(requisition.RequestorUserId, currentUserId, StringComparison.Ordinal))
            return Forbid();

        if (requisition.Status != RequisitionStatus.Pending)
        {
            TempData["ErrorMessage"] = "Only pending requisitions can be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        model.Date = DateOnly.FromDateTime(DateTime.Today);
        model.RequestorName = User.FindFirstValue(SmrsClaimTypes.EmployeeName) ?? model.RequestorName;
        model.RequestorPosition = User.FindFirstValue(SmrsClaimTypes.Position) ?? model.RequestorPosition;
        model.RequestorDivision = User.FindFirstValue(SmrsClaimTypes.Division) ?? model.RequestorDivision;
        model.RsNo = (model.RsNo ?? string.Empty).Trim().ToUpperInvariant();
        model.Items ??= [];
        model.Items = model.Items.Where(i => i is not null).ToList();

        foreach (var item in model.Items)
            item.RfNo = string.IsNullOrWhiteSpace(item.RfNo) ? null : item.RfNo.Trim();

        if (model.Items.Count == 0)
            ModelState.AddModelError(nameof(model.Items), "Add at least one item.");

        if (!ModelState.IsValid)
        {
            ViewData["FormAction"] = nameof(Edit);
            ViewData["EditId"] = id;
            ViewData["SubmitLabel"] = "Save changes";
            ViewData["PageHeading"] = "Edit requisition slip";
            return View(nameof(Create), model);
        }

        await using (var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
        {
            var releaseError = await RequisitionInventoryReservation.ReleaseReservationForRejectedAsync(
                _db, requisition, cancellationToken);
            if (releaseError is not null)
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                TempData["ErrorMessage"] = releaseError;
                return RedirectToAction(nameof(Details), new { id });
            }

            _db.RequisitionRecordItems.RemoveRange(requisition.Items);
            await _db.SaveChangesAsync(cancellationToken);

            var reserveError = await RequisitionInventoryReservation.ApplyReservationForNewRequisitionAsync(
                _db, model.ItemType, model.Items, cancellationToken);
            if (reserveError is not null)
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                ModelState.AddModelError(string.Empty, reserveError);
                ViewData["FormAction"] = nameof(Edit);
                ViewData["EditId"] = id;
                ViewData["SubmitLabel"] = "Save changes";
                ViewData["PageHeading"] = "Edit requisition slip";
                return View(nameof(Create), model);
            }

            requisition.RsNo = model.RsNo;
            requisition.ItemType = model.ItemType;
            requisition.Date = model.Date;
            requisition.RequestorName = model.RequestorName.Trim();
            requisition.RequestorPosition = model.RequestorPosition.Trim();
            requisition.RequestorDivision = model.RequestorDivision.Trim();
            requisition.Office = string.IsNullOrWhiteSpace(model.Office) ? null : model.Office.Trim();
            requisition.MrIcsPosition = string.IsNullOrWhiteSpace(model.MrIcsPosition) ? null : model.MrIcsPosition.Trim();
            requisition.Items = model.Items.Select(i => new RequisitionRecordItem
            {
                InventoryItemId = i.InventoryItemId,
                SerialNo = string.IsNullOrWhiteSpace(i.SerialNo) ? null : i.SerialNo.Trim(),
                Qty = i.Qty,
                Unit = i.Unit.Trim(),
                Purpose = i.Purpose.Trim(),
                RfNo = string.IsNullOrWhiteSpace(i.RfNo) ? null : i.RfNo.Trim()
            }).ToList();

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }

        TempData["StatusMessage"] = "Requisition updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    [Authorize(Policy = SmrsPolicies.RequisitionApproval)]
    public async Task<IActionResult> Approvals(CancellationToken cancellationToken)
    {
        var query = _db.RequisitionRecords
            .AsNoTracking()
            .Include(r => r.Items)
            .OrderBy(r => r.CreatedAt)
            .ApplyApproverQueueFilter(User);

        var rows = await query
            .Select(r => new RequisitionApprovalRowViewModel
            {
                Id = r.Id,
                RsNo = r.RsNo,
                RequestorName = r.RequestorName,
                RequestorDivision = r.RequestorDivision,
                ItemType = r.ItemType,
                ItemCount = r.Items.Count,
                Date = r.Date,
                Status = r.Status,
                MarkedInUseAt = r.MarkedInUseAt
            })
            .ToListAsync(cancellationToken);

        return View(rows);
    }

    /// <summary>Approved requisitions: record physical issuance (sets <see cref="RequisitionRecord.MarkedInUseAt"/>).</summary>
    [HttpGet]
    [Authorize(Policy = SmrsPolicies.RequisitionChecker)]
    public async Task<IActionResult> Issuance(CancellationToken cancellationToken)
    {
        var rows = await _db.RequisitionRecords
            .AsNoTracking()
            .Include(r => r.Items)
            .Where(r => r.Status == RequisitionStatus.Approved)
            .OrderBy(r => r.MarkedInUseAt == null ? 0 : 1)
            .ThenBy(r => r.CreatedAt)
            .Select(r => new RequisitionApprovalRowViewModel
            {
                Id = r.Id,
                RsNo = r.RsNo,
                RequestorName = r.RequestorName,
                RequestorDivision = r.RequestorDivision,
                ItemType = r.ItemType,
                ItemCount = r.Items.Count,
                Date = r.Date,
                Status = r.Status,
                MarkedInUseAt = r.MarkedInUseAt
            })
            .ToListAsync(cancellationToken);

        return View(rows);
    }

    [HttpGet]
    [Authorize(Policy = SmrsPolicies.RequisitionChecker)]
    public async Task<IActionResult> Checker(CancellationToken cancellationToken)
    {
        var rows = await _db.RequisitionRecords
            .AsNoTracking()
            .Include(r => r.Items)
            .Where(r => r.Status == RequisitionStatus.Approved)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new RequisitionApprovalRowViewModel
            {
                Id = r.Id,
                RsNo = r.RsNo,
                RequestorName = r.RequestorName,
                RequestorDivision = r.RequestorDivision,
                ItemType = r.ItemType,
                ItemCount = r.Items.Count,
                Date = r.Date,
                Status = r.Status,
                MarkedInUseAt = r.MarkedInUseAt
            })
            .ToListAsync(cancellationToken);

        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, string? returnAction, CancellationToken cancellationToken)
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
        var canViewAsApprover = canApprove
            && RequisitionApproverScope.CanApproveItemType(User, requisition.ItemType);
        var isRequestOwner = !string.IsNullOrWhiteSpace(currentUserId)
            && string.Equals(requisition.RequestorUserId, currentUserId, StringComparison.Ordinal);
        var canViewAsChecker = (User.IsInRole(SmrsRoles.Encoder) || User.IsInRole(SmrsRoles.DepartmentHead))
            && requisition.Status == RequisitionStatus.Approved;

        if (!canViewAsApprover && !isRequestOwner && !canViewAsChecker)
            return Forbid();

        ViewData["CanTakeAction"] = canViewAsApprover && requisition.Status == RequisitionStatus.Pending;
        ViewData["CanEdit"] = isRequestOwner && requisition.Status == RequisitionStatus.Pending;

        var canUseIssuanceNav = User.IsInRole(SmrsRoles.Encoder) || User.IsInRole(SmrsRoles.DepartmentHead);
        if (canViewAsApprover)
            ViewData["BackAction"] = nameof(Approvals);
        else if (requisition.Status == RequisitionStatus.Approved && canUseIssuanceNav)
        {
            if (string.Equals(returnAction, nameof(Issuance), StringComparison.OrdinalIgnoreCase))
                ViewData["BackAction"] = nameof(Issuance);
            else if (string.Equals(returnAction, nameof(Checker), StringComparison.OrdinalIgnoreCase))
                ViewData["BackAction"] = nameof(Checker);
            else if (User.IsInRole(SmrsRoles.Encoder) && !isRequestOwner)
                ViewData["BackAction"] = nameof(Checker);
            else
                ViewData["BackAction"] = nameof(Index);
        }
        else
            ViewData["BackAction"] = nameof(Index);

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
            MarkedInUseAt = requisition.MarkedInUseAt,
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
        await using (var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
        {
            var requisition = await _db.RequisitionRecords
                .Include(r => r.Items)
                .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (requisition is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return NotFound();
            }

            if (!RequisitionApproverScope.CanApproveItemType(User, requisition.ItemType))
            {
                await tx.RollbackAsync(cancellationToken);
                return Forbid();
            }

            if (requisition.Status != RequisitionStatus.Pending)
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                TempData["ErrorMessage"] = "This requisition is already processed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var fulfillError = await RequisitionInventoryReservation.FulfillApprovedAsync(_db, requisition, cancellationToken);
            if (fulfillError is not null)
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                TempData["ErrorMessage"] = fulfillError;
                return RedirectToAction(nameof(Details), new { id });
            }

            requisition.Status = RequisitionStatus.Approved;
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }

        TempData["StatusMessage"] = "Requisition approved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SmrsPolicies.RequisitionApproval)]
    public async Task<IActionResult> Reject(int id, CancellationToken cancellationToken)
    {
        await using (var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
        {
            var requisition = await _db.RequisitionRecords
                .Include(r => r.Items)
                .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (requisition is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return NotFound();
            }

            if (!RequisitionApproverScope.CanApproveItemType(User, requisition.ItemType))
            {
                await tx.RollbackAsync(cancellationToken);
                return Forbid();
            }

            if (requisition.Status != RequisitionStatus.Pending)
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                TempData["ErrorMessage"] = "This requisition is already processed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var releaseError = await RequisitionInventoryReservation.ReleaseReservationForRejectedAsync(
                _db, requisition, cancellationToken);
            if (releaseError is not null)
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                TempData["ErrorMessage"] = releaseError;
                return RedirectToAction(nameof(Details), new { id });
            }

            requisition.Status = RequisitionStatus.Rejected;
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }

        TempData["StatusMessage"] = "Requisition rejected.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SmrsPolicies.RequisitionChecker)]
    public async Task<IActionResult> MarkInUse(int id, string? returnAction, CancellationToken cancellationToken)
    {
        var requisition = await _db.RequisitionRecords.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (requisition is null)
            return NotFound();

        if (requisition.Status != RequisitionStatus.Approved)
        {
            TempData["ErrorMessage"] = "Only approved requisitions can be recorded as issued.";
            return RedirectToAction(nameof(Details), new { id, returnAction });
        }

        if (requisition.MarkedInUseAt is not null)
        {
            TempData["ErrorMessage"] = "This requisition is already recorded as in use.";
            return RedirectToAction(nameof(Details), new { id, returnAction });
        }

        requisition.MarkedInUseAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        TempData["StatusMessage"] = "Issuance recorded; items are in use with the requestor.";

        if (string.Equals(returnAction, nameof(Issuance), StringComparison.OrdinalIgnoreCase))
            return RedirectToAction(nameof(Issuance));

        return RedirectToAction(nameof(Details), new { id, returnAction });
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
                brand = i.Brand,
                specification = string.IsNullOrWhiteSpace(i.Description) ? i.Specifications : i.Description,
                displayName = string.Join(" - ", new[]
                {
                    i.ItemName,
                    string.IsNullOrWhiteSpace(i.Brand) ? null : i.Brand,
                    string.IsNullOrWhiteSpace(i.Description) ? i.Specifications : i.Description
                }.Where(part => !string.IsNullOrWhiteSpace(part))),
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
