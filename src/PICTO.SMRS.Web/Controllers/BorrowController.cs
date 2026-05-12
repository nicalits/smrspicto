using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Models.Inventory;
using PICTO.SMRS.Web.Models.Borrow;
using PICTO.SMRS.Web.Security;
using PICTO.SMRS.Web.Services;

namespace PICTO.SMRS.Web.Controllers;

[Authorize]
public class BorrowController : Controller
{
    private readonly ApplicationDbContext _db;

    public BorrowController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId))
            return View(Array.Empty<BorrowIndexRowViewModel>());

        var rows = await _db.BorrowRecords
            .AsNoTracking()
            .Where(r => r.BorrowerUserId == currentUserId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new BorrowIndexRowViewModel
            {
                Id = r.Id,
                RfNo = r.RfNo,
                SlipDate = r.SlipDate,
                ItemCount = r.Items.Count,
                Status = r.Status,
                IssuedAt = r.IssuedAt
            })
            .ToListAsync(cancellationToken);

        return View(rows);
    }

    [HttpGet]
    public IActionResult Create()
    {
        var vm = new BorrowCreateViewModel
        {
            SlipDate = DateOnly.FromDateTime(DateTime.Today),
            BorrowerName = User.FindFirstValue(SmrsClaimTypes.EmployeeName) ?? string.Empty,
            BorrowerDivision = User.FindFirstValue(SmrsClaimTypes.Division) ?? string.Empty,
            Items = [new BorrowLineItemViewModel()]
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var borrow = await _db.BorrowRecords
            .AsNoTracking()
            .Include(r => r.Items)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (borrow is null)
            return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId)
            || !string.Equals(borrow.BorrowerUserId, currentUserId, StringComparison.Ordinal))
            return Forbid();

        if (borrow.Status != BorrowStatus.InQueue)
        {
            TempData["ErrorMessage"] = "Only queued borrow requests can be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new BorrowCreateViewModel
        {
            RfNo = borrow.RfNo,
            BorrowerName = borrow.BorrowerName,
            BorrowerDivision = borrow.BorrowerDivision,
            Office = borrow.Office,
            SlipDate = borrow.SlipDate,
            SlipTime = borrow.SlipTime,
            TelNo = borrow.TelNo,
            Remarks = borrow.Remarks,
            Items = borrow.Items.Select(i => new BorrowLineItemViewModel
            {
                InventoryItemId = i.InventoryItemId,
                Description = i.Description,
                Qty = i.Qty,
                LocationVenue = i.LocationVenue,
                Purpose = i.Purpose,
                BorrowDate = i.BorrowDate,
                BorrowTime = i.BorrowTime,
                ReturnDate = i.ReturnDate,
                ReturnTime = i.ReturnTime
            }).ToList()
        };

        ViewData["FormAction"] = nameof(Edit);
        ViewData["EditId"] = id;
        ViewData["SubmitLabel"] = "Save changes";
        ViewData["PageHeading"] = "Edit borrower's slip";
        return View(nameof(Create), model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BorrowCreateViewModel model, CancellationToken cancellationToken)
    {
        model.SlipDate = DateOnly.FromDateTime(DateTime.Today);
        model.BorrowerName = User.FindFirstValue(SmrsClaimTypes.EmployeeName) ?? model.BorrowerName;
        model.BorrowerDivision = User.FindFirstValue(SmrsClaimTypes.Division) ?? model.BorrowerDivision;
        model.RfNo = string.IsNullOrWhiteSpace(model.RfNo) ? null : model.RfNo.Trim().ToUpperInvariant();

        model.Items ??= [];
        model.Items = model.Items.Where(i => i is not null).ToList();

        foreach (var item in model.Items)
        {
            item.Description = (item.Description ?? string.Empty).Trim();
            item.LocationVenue = (item.LocationVenue ?? string.Empty).Trim();
            item.Purpose = (item.Purpose ?? string.Empty).Trim();
            item.BorrowTime = string.IsNullOrWhiteSpace(item.BorrowTime) ? null : item.BorrowTime.Trim();
            item.ReturnTime = string.IsNullOrWhiteSpace(item.ReturnTime) ? null : item.ReturnTime.Trim();
        }

        model.Office = string.IsNullOrWhiteSpace(model.Office) ? null : model.Office.Trim();
        model.TelNo = string.IsNullOrWhiteSpace(model.TelNo) ? null : model.TelNo.Trim();
        model.Remarks = string.IsNullOrWhiteSpace(model.Remarks) ? null : model.Remarks.Trim();
        model.SlipTime = string.IsNullOrWhiteSpace(model.SlipTime) ? null : model.SlipTime.Trim();

        if (model.Items.Count == 0)
            ModelState.AddModelError(nameof(model.Items), "Add at least one item.");

        if (!ModelState.IsValid)
            return View(model);

        var stockError = await BorrowInventoryStock.ValidateNewBorrowStockAsync(_db, model.Items, cancellationToken);
        if (stockError is not null)
        {
            ModelState.AddModelError(string.Empty, stockError);
            return View(model);
        }

        var borrow = new BorrowRecord
        {
            RfNo = model.RfNo,
            BorrowerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            BorrowerName = model.BorrowerName.Trim(),
            BorrowerDivision = model.BorrowerDivision.Trim(),
            Office = model.Office,
            SlipDate = model.SlipDate,
            SlipTime = model.SlipTime,
            TelNo = model.TelNo,
            Remarks = model.Remarks,
            Status = BorrowStatus.InQueue,
            CreatedAt = DateTimeOffset.UtcNow,
            Items = model.Items.Select(i => new BorrowRecordItem
            {
                InventoryItemId = i.InventoryItemId,
                Description = i.Description,
                Qty = i.Qty,
                LocationVenue = i.LocationVenue,
                Purpose = i.Purpose,
                BorrowDate = i.BorrowDate,
                BorrowTime = i.BorrowTime,
                ReturnDate = i.ReturnDate,
                ReturnTime = i.ReturnTime
            }).ToList()
        };

        _db.BorrowRecords.Add(borrow);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = "Borrow request submitted for encoder approval.";
        return RedirectToAction(nameof(Details), new { id = borrow.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, BorrowCreateViewModel model, CancellationToken cancellationToken)
    {
        var borrow = await _db.BorrowRecords
            .Include(r => r.Items)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (borrow is null)
            return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId)
            || !string.Equals(borrow.BorrowerUserId, currentUserId, StringComparison.Ordinal))
            return Forbid();

        if (borrow.Status != BorrowStatus.InQueue)
        {
            TempData["ErrorMessage"] = "Only queued borrow requests can be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        model.SlipDate = DateOnly.FromDateTime(DateTime.Today);
        model.BorrowerName = User.FindFirstValue(SmrsClaimTypes.EmployeeName) ?? model.BorrowerName;
        model.BorrowerDivision = User.FindFirstValue(SmrsClaimTypes.Division) ?? model.BorrowerDivision;
        model.RfNo = string.IsNullOrWhiteSpace(model.RfNo) ? null : model.RfNo.Trim().ToUpperInvariant();

        model.Items ??= [];
        model.Items = model.Items.Where(i => i is not null).ToList();

        foreach (var item in model.Items)
        {
            item.Description = (item.Description ?? string.Empty).Trim();
            item.LocationVenue = (item.LocationVenue ?? string.Empty).Trim();
            item.Purpose = (item.Purpose ?? string.Empty).Trim();
            item.BorrowTime = string.IsNullOrWhiteSpace(item.BorrowTime) ? null : item.BorrowTime.Trim();
            item.ReturnTime = string.IsNullOrWhiteSpace(item.ReturnTime) ? null : item.ReturnTime.Trim();
        }

        model.Office = string.IsNullOrWhiteSpace(model.Office) ? null : model.Office.Trim();
        model.TelNo = string.IsNullOrWhiteSpace(model.TelNo) ? null : model.TelNo.Trim();
        model.Remarks = string.IsNullOrWhiteSpace(model.Remarks) ? null : model.Remarks.Trim();
        model.SlipTime = string.IsNullOrWhiteSpace(model.SlipTime) ? null : model.SlipTime.Trim();

        if (model.Items.Count == 0)
            ModelState.AddModelError(nameof(model.Items), "Add at least one item.");

        if (!ModelState.IsValid)
        {
            ViewData["FormAction"] = nameof(Edit);
            ViewData["EditId"] = id;
            ViewData["SubmitLabel"] = "Save changes";
            ViewData["PageHeading"] = "Edit borrower's slip";
            return View(nameof(Create), model);
        }

        var stockError = await BorrowInventoryStock.ValidateNewBorrowStockAsync(_db, model.Items, cancellationToken);
        if (stockError is not null)
        {
            ModelState.AddModelError(string.Empty, stockError);
            ViewData["FormAction"] = nameof(Edit);
            ViewData["EditId"] = id;
            ViewData["SubmitLabel"] = "Save changes";
            ViewData["PageHeading"] = "Edit borrower's slip";
            return View(nameof(Create), model);
        }

        _db.BorrowRecordItems.RemoveRange(borrow.Items);

        borrow.RfNo = model.RfNo;
        borrow.BorrowerName = model.BorrowerName.Trim();
        borrow.BorrowerDivision = model.BorrowerDivision.Trim();
        borrow.Office = model.Office;
        borrow.SlipDate = model.SlipDate;
        borrow.SlipTime = model.SlipTime;
        borrow.TelNo = model.TelNo;
        borrow.Remarks = model.Remarks;
        borrow.Items = model.Items.Select(i => new BorrowRecordItem
        {
            InventoryItemId = i.InventoryItemId,
            Description = i.Description,
            Qty = i.Qty,
            LocationVenue = i.LocationVenue,
            Purpose = i.Purpose,
            BorrowDate = i.BorrowDate,
            BorrowTime = i.BorrowTime,
            ReturnDate = i.ReturnDate,
            ReturnTime = i.ReturnTime
        }).ToList();

        await _db.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = "Borrow request updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    [Authorize(Policy = SmrsPolicies.BorrowApproval)]
    public async Task<IActionResult> Approvals(CancellationToken cancellationToken)
    {
        var rows = await _db.BorrowRecords
            .AsNoTracking()
            .Include(r => r.Items)
            .Where(r => r.Status == BorrowStatus.InQueue || r.Status == BorrowStatus.Pending)
            .OrderBy(r => r.Status == BorrowStatus.InQueue ? 0 : 1)
            .ThenBy(r => r.CreatedAt)
            .Select(r => new BorrowApprovalRowViewModel
            {
                Id = r.Id,
                RfNo = r.RfNo,
                BorrowerName = r.BorrowerName,
                BorrowerDivision = r.BorrowerDivision,
                SlipDate = r.SlipDate,
                ItemCount = r.Items.Count,
                Status = r.Status,
                PendingReason = r.PendingReason,
                IssuedAt = r.IssuedAt
            })
            .ToListAsync(cancellationToken);

        var queuePositions = await QueuePositionHelper.GetBorrowQueuePositionsAsync(_db, cancellationToken);
        ViewData["QueuePositions"] = queuePositions;

        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var borrow = await _db.BorrowRecords
            .AsNoTracking()
            .Include(r => r.Items)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (borrow is null)
            return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isRequestOwner = !string.IsNullOrWhiteSpace(currentUserId)
            && string.Equals(borrow.BorrowerUserId, currentUserId, StringComparison.Ordinal);
        var canView = User.IsInRole(SmrsRoles.Admin) || User.IsInRole(SmrsRoles.Encoder)
            || User.IsInRole(SmrsRoles.DepartmentHead);
        var canAct = User.IsInRole(SmrsRoles.Admin) || User.IsInRole(SmrsRoles.Encoder);

        if (!isRequestOwner && !canView)
            return Forbid();

        ViewData["CanTakeAction"] = canAct
            && (borrow.Status == BorrowStatus.InQueue || borrow.Status == BorrowStatus.Pending);
        ViewData["CanEdit"] = isRequestOwner && borrow.Status == BorrowStatus.InQueue;
        ViewData["BackAction"] = canView && !isRequestOwner ? nameof(Approvals) : nameof(Index);

        var queuePositions = await QueuePositionHelper.GetBorrowQueuePositionsAsync(_db, cancellationToken);
        ViewData["QueuePositions"] = queuePositions;

        return View(new BorrowDetailsViewModel
        {
            Id = borrow.Id,
            RfNo = borrow.RfNo,
            BorrowerName = borrow.BorrowerName,
            BorrowerDivision = borrow.BorrowerDivision,
            Office = borrow.Office,
            SlipDate = borrow.SlipDate,
            SlipTime = borrow.SlipTime,
            TelNo = borrow.TelNo,
            Remarks = borrow.Remarks,
            Status = borrow.Status,
            PendingReason = borrow.PendingReason,
            RejectionReason = borrow.RejectionReason,
            ApprovedAt = borrow.ApprovedAt,
            RejectedAt = borrow.RejectedAt,
            IssuedAt = borrow.IssuedAt,
            Items = borrow.Items.Select(i => new BorrowDetailsItemViewModel
            {
                Description = i.Description,
                Qty = i.Qty,
                LocationVenue = i.LocationVenue,
                Purpose = i.Purpose,
                BorrowDate = i.BorrowDate,
                BorrowTime = i.BorrowTime,
                ReturnDate = i.ReturnDate,
                ReturnTime = i.ReturnTime
            }).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SmrsPolicies.BorrowApprovalAction)]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        await using (var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
        {
            var borrow = await _db.BorrowRecords
                .Include(r => r.Items)
                .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (borrow is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return NotFound();
            }

            if (borrow.Status != BorrowStatus.InQueue && borrow.Status != BorrowStatus.Pending)
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                TempData["ErrorMessage"] = "This borrow request is already processed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (borrow.Items.Any(i => !i.InventoryItemId.HasValue))
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                TempData["ErrorMessage"] = "Every borrowed item must be linked to inventory before approval.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var requestedByItemId = borrow.Items
                .GroupBy(i => i.InventoryItemId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Qty));

            var inventoryItems = await _db.InventoryItems
                .Where(i => requestedByItemId.Keys.Contains(i.Id))
                .ToListAsync(cancellationToken);

            if (inventoryItems.Count != requestedByItemId.Count)
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                TempData["ErrorMessage"] = "One or more selected inventory items could not be found.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var unavailableByItemId = await InventoryAvailability.GetUnavailableQuantitiesAsync(
                _db,
                requestedByItemId.Keys.ToList(),
                cancellationToken);

            foreach (var inv in inventoryItems)
            {
                var requestedQty = requestedByItemId[inv.Id];
                var available = Math.Max(0, inv.Quantity - unavailableByItemId.GetValueOrDefault(inv.Id));
                if (requestedQty > available)
                {
                    await tx.RollbackAsync(cancellationToken);
                    _db.ChangeTracker.Clear();
                    TempData["ErrorMessage"] = $"Not enough available stock for \"{inv.ItemName}\" (available {available}, requested {requestedQty}).";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            var now = DateTimeOffset.UtcNow;
            borrow.Status = BorrowStatus.Approved;
            borrow.ApprovedAt = now;
            borrow.IssuedAt = now;
            borrow.PendingReason = null;
            borrow.ActionedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _db.SaveChangesAsync(cancellationToken);
            var affectedItemIds = borrow.Items
                .Where(i => i.InventoryItemId.HasValue)
                .Select(i => i.InventoryItemId!.Value)
                .Distinct()
                .ToList();
            await LowStockTracker.RefreshAsync(_db, affectedItemIds, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }

        TempData["StatusMessage"] = "Borrow request approved and issued.";
        return RedirectToAction(nameof(Approvals));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SmrsPolicies.BorrowApprovalAction)]
    public async Task<IActionResult> Reject(int id, string? reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["ErrorMessage"] = "A reason is required when rejecting a borrow request.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var borrow = await _db.BorrowRecords.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (borrow is null)
            return NotFound();

        if (borrow.Status != BorrowStatus.InQueue && borrow.Status != BorrowStatus.Pending)
        {
            TempData["ErrorMessage"] = "This borrow request is already processed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        borrow.Status = BorrowStatus.Rejected;
        borrow.RejectionReason = reason.Trim();
        borrow.PendingReason = null;
        borrow.RejectedAt = DateTimeOffset.UtcNow;
        borrow.ActionedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = "Borrow request rejected.";
        return RedirectToAction(nameof(Approvals));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SmrsPolicies.BorrowApprovalAction)]
    public async Task<IActionResult> Hold(int id, string? reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["ErrorMessage"] = "A reason is required when putting a borrow request on hold.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var borrow = await _db.BorrowRecords.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (borrow is null)
            return NotFound();

        if (borrow.Status != BorrowStatus.InQueue)
        {
            TempData["ErrorMessage"] = "Only queued borrow requests can be put on hold.";
            return RedirectToAction(nameof(Details), new { id });
        }

        borrow.Status = BorrowStatus.Pending;
        borrow.PendingReason = reason.Trim();
        borrow.ActionedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = "Borrow request placed on hold.";
        return RedirectToAction(nameof(Approvals));
    }

    [HttpGet]
    [Authorize(Policy = SmrsPolicies.BorrowApproval)]
    public async Task<IActionResult> Rejected(CancellationToken cancellationToken)
    {
        var rows = await _db.BorrowRecords
            .AsNoTracking()
            .Include(r => r.Items)
            .Where(r => r.Status == BorrowStatus.Rejected)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new BorrowApprovalRowViewModel
            {
                Id = r.Id,
                RfNo = r.RfNo,
                BorrowerName = r.BorrowerName,
                BorrowerDivision = r.BorrowerDivision,
                SlipDate = r.SlipDate,
                ItemCount = r.Items.Count,
                Status = r.Status,
                RejectionReason = r.RejectionReason,
                IssuedAt = r.IssuedAt
            })
            .ToListAsync(cancellationToken);

        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> InventoryItems(CancellationToken cancellationToken)
    {
        var items = await _db.InventoryItems
            .AsNoTracking()
            .Where(i => i.SupplyGroup == SupplyGroup.ItSupplies)
            .OrderBy(i => i.ItemName)
            .Select(i => new
            {
                id = i.Id,
                name = i.ItemName,
                brand = i.Brand,
                specification = string.IsNullOrWhiteSpace(i.Description) ? i.Specifications : i.Description,
                unit = i.Unit.GetDisplayName(),
                supplyGroup = i.SupplyGroup
            })
            .ToListAsync(cancellationToken);

        return Json(items);
    }
}
