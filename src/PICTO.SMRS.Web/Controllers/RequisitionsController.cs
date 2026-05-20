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
                MarkedInUseAt = r.MarkedInUseAt,
                ReceivedAt = r.ReceivedAt
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
            Status = RequisitionStatus.InQueue,
            CreatedAt = DateTimeOffset.UtcNow,
            Items = []
        };

        await using (var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
        {
            var reserveError = await RequisitionInventoryStock.ValidateNewRequisitionStockAsync(
                _db, model.ItemType, model.Items, cancellationToken);
            if (reserveError is not null)
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                ModelState.AddModelError(string.Empty, reserveError);
                return View(model);
            }

            _db.RequisitionRecords.Add(requisition);
            requisition.Items = await BuildRecordItemsAsync(model.Items, cancellationToken);
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

        if (requisition.Status != RequisitionStatus.InQueue)
        {
            TempData["ErrorMessage"] = "Only queued requisitions can be edited.";
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

        if (requisition.Status != RequisitionStatus.InQueue)
        {
            TempData["ErrorMessage"] = "Only queued requisitions can be edited.";
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
            _db.RequisitionRecordItems.RemoveRange(requisition.Items);
            await _db.SaveChangesAsync(cancellationToken);

            var reserveError = await RequisitionInventoryStock.ValidateNewRequisitionStockAsync(
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
            requisition.Items = await BuildRecordItemsAsync(model.Items, cancellationToken);

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
        ViewData["CanApprove"] = User.IsInRole(SmrsRoles.Admin)
            || User.IsInRole(SmrsRoles.ItDivisionHead)
            || User.IsInRole(SmrsRoles.OfficeDivisionHead);

        var query = _db.RequisitionRecords
            .AsNoTracking()
            .Include(r => r.Items)
            .Where(r => r.Status == RequisitionStatus.InQueue || r.Status == RequisitionStatus.Pending)
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
                PendingReason = r.PendingReason,
                MarkedInUseAt = r.MarkedInUseAt
            })
            .ToListAsync(cancellationToken);

        var queuePositions = await QueuePositionHelper.GetRequisitionQueuePositionsAsync(_db, cancellationToken);
        ViewData["QueuePositions"] = queuePositions;

        return View(rows);
    }

    /// <summary>Approved requisitions: record physical issuance (sets <see cref="RequisitionRecord.MarkedInUseAt"/>).</summary>
    [HttpGet]
    [Authorize(Policy = SmrsPolicies.RequisitionChecker)]
    public async Task<IActionResult> Issuance(CancellationToken cancellationToken)
    {
        ViewData["CanIssue"] = User.IsInRole(SmrsRoles.Admin) || User.IsInRole(SmrsRoles.Encoder);

        var rows = await _db.RequisitionRecords
            .AsNoTracking()
            .Include(r => r.Items)
            .Where(r => r.Status == RequisitionStatus.Approved && r.MarkedInUseAt == null)
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
    [Authorize(Policy = SmrsPolicies.RequisitionChecker)]
    public async Task<IActionResult> Checker(CancellationToken cancellationToken)
    {
        var rows = await _db.RequisitionRecords
            .AsNoTracking()
            .Include(r => r.Items)
            .Where(r => r.Status == RequisitionStatus.Approved && r.MarkedInUseAt == null)
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
        var canViewApproval = User.IsInRole(SmrsRoles.Admin) || User.IsInRole(SmrsRoles.DepartmentHead)
            || User.IsInRole(SmrsRoles.ItDivisionHead) || User.IsInRole(SmrsRoles.OfficeDivisionHead);
        var canActOnApproval = RequisitionApproverScope.CanApproveItemType(User, requisition.ItemType);
        var canViewThisRequisition = canViewApproval
            && RequisitionApproverScope.CanViewItemType(User, requisition.ItemType);
        var isRequestOwner = !string.IsNullOrWhiteSpace(currentUserId)
            && string.Equals(requisition.RequestorUserId, currentUserId, StringComparison.Ordinal);
        var canViewAsChecker = (User.IsInRole(SmrsRoles.Admin) || User.IsInRole(SmrsRoles.Encoder)
            || User.IsInRole(SmrsRoles.DepartmentHead))
            && requisition.Status == RequisitionStatus.Approved;

        if (!canViewThisRequisition && !isRequestOwner && !canViewAsChecker)
            return Forbid();

        ViewData["CanTakeAction"] = canActOnApproval
            && (requisition.Status == RequisitionStatus.InQueue || requisition.Status == RequisitionStatus.Pending);
        ViewData["CanCancelApproval"] = canActOnApproval
            && requisition.Status == RequisitionStatus.Approved
            && requisition.MarkedInUseAt is null;
        ViewData["CanEdit"] = isRequestOwner && requisition.Status == RequisitionStatus.InQueue;
        ViewData["CanMarkReceived"] = isRequestOwner
            && requisition.Status == RequisitionStatus.Approved
            && requisition.MarkedInUseAt is not null
            && requisition.ReceivedAt is null;

        var canUseIssuanceNav = User.IsInRole(SmrsRoles.Admin) || User.IsInRole(SmrsRoles.Encoder)
            || User.IsInRole(SmrsRoles.DepartmentHead);
        if (canViewThisRequisition)
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

        var queuePositions = await QueuePositionHelper.GetRequisitionQueuePositionsAsync(_db, cancellationToken);
        ViewData["QueuePositions"] = queuePositions;

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
            PendingReason = requisition.PendingReason,
            RejectionReason = requisition.RejectionReason,
            MarkedInUseAt = requisition.MarkedInUseAt,
            ReceivedAt = requisition.ReceivedAt,
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
    [Authorize(Policy = SmrsPolicies.RequisitionApprovalAction)]
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

            if (requisition.Status != RequisitionStatus.InQueue && requisition.Status != RequisitionStatus.Pending)
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                TempData["ErrorMessage"] = "This requisition is already processed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var fulfillError = await RequisitionInventoryStock.FulfillApprovedAsync(_db, requisition, cancellationToken);
            if (fulfillError is not null)
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                TempData["ErrorMessage"] = fulfillError;
                return RedirectToAction(nameof(Details), new { id });
            }

            requisition.Status = RequisitionStatus.Approved;
            requisition.PendingReason = null;
            requisition.ActionedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _db.SaveChangesAsync(cancellationToken);
            await LowStockTracker.RefreshAsync(_db, requisition.Items.Select(i => i.InventoryItemId).Distinct().ToList(), cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }

        TempData["StatusMessage"] = "Requisition approved.";
        return RedirectToAction(nameof(Approvals));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SmrsPolicies.RequisitionApprovalAction)]
    public async Task<IActionResult> Reject(int id, string? reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["ErrorMessage"] = "A reason is required when rejecting a requisition.";
            return RedirectToAction(nameof(Details), new { id });
        }

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

            if (requisition.Status != RequisitionStatus.InQueue && requisition.Status != RequisitionStatus.Pending)
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                TempData["ErrorMessage"] = "This requisition is already processed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            requisition.Status = RequisitionStatus.Rejected;
            requisition.RejectionReason = reason.Trim();
            requisition.PendingReason = null;
            requisition.ActionedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }

        TempData["StatusMessage"] = "Requisition rejected.";
        return RedirectToAction(nameof(Approvals));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SmrsPolicies.RequisitionApprovalAction)]
    public async Task<IActionResult> Hold(int id, string? reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["ErrorMessage"] = "A reason is required when putting a requisition on hold.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var requisition = await _db.RequisitionRecords
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (requisition is null)
            return NotFound();

        if (!RequisitionApproverScope.CanApproveItemType(User, requisition.ItemType))
            return Forbid();

        if (requisition.Status != RequisitionStatus.InQueue)
        {
            TempData["ErrorMessage"] = "Only queued requisitions can be put on hold.";
            return RedirectToAction(nameof(Details), new { id });
        }

        requisition.Status = RequisitionStatus.Pending;
        requisition.PendingReason = reason.Trim();
        requisition.ActionedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = "Requisition placed on hold.";
        return RedirectToAction(nameof(Approvals));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SmrsPolicies.RequisitionApprovalAction)]
    public async Task<IActionResult> CancelApproval(int id, CancellationToken cancellationToken)
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

            if (requisition.Status != RequisitionStatus.Approved)
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                TempData["ErrorMessage"] = "Only approved requisitions can have approval canceled.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (requisition.MarkedInUseAt is not null)
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                TempData["ErrorMessage"] = "Approval cannot be canceled after checker issuance.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var restoreError = await RequisitionInventoryStock.RestoreFulfilledApprovalAsync(
                _db, requisition, cancellationToken);
            if (restoreError is not null)
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                TempData["ErrorMessage"] = restoreError;
                return RedirectToAction(nameof(Details), new { id });
            }

            requisition.Status = RequisitionStatus.InQueue;
            requisition.PendingReason = null;
            requisition.RejectionReason = null;
            await _db.SaveChangesAsync(cancellationToken);
            await LowStockTracker.RefreshAsync(_db, requisition.Items.Select(i => i.InventoryItemId).Distinct().ToList(), cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }

        TempData["StatusMessage"] = "Approval canceled; requisition returned to queue.";
        return RedirectToAction(nameof(Approvals));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SmrsPolicies.RequisitionCheckerAction)]
    public async Task<IActionResult> MarkInUse(int id, string? returnAction, CancellationToken cancellationToken)
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

            if (requisition.Status != RequisitionStatus.Approved)
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                TempData["ErrorMessage"] = "Only approved requisitions can be recorded as issued.";
                return RedirectToAction(nameof(Details), new { id, returnAction });
            }

            if (requisition.MarkedInUseAt is not null)
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                TempData["ErrorMessage"] = "This requisition is already recorded as issued.";
                return RedirectToAction(nameof(Details), new { id, returnAction });
            }

            var issueError = await RequisitionInventoryStock.FulfillApprovedAsync(_db, requisition, cancellationToken);
            if (issueError is not null)
            {
                await tx.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                TempData["ErrorMessage"] = issueError;
                return RedirectToAction(nameof(Details), new { id, returnAction });
            }

            requisition.MarkedInUseAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await LowStockTracker.RefreshAsync(_db, requisition.Items.Select(i => i.InventoryItemId).Distinct().ToList(), cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }

        TempData["StatusMessage"] = "Issuance recorded; items issued to the requestor.";

        if (string.Equals(returnAction, nameof(Issuance), StringComparison.OrdinalIgnoreCase))
            return RedirectToAction(nameof(Issuance));

        return RedirectToAction(nameof(Details), new { id, returnAction });
    }

    [HttpGet]
    public async Task<IActionResult> InventoryItems(RequisitionItemType type, CancellationToken cancellationToken)
    {
        var group = type == RequisitionItemType.OfficeSupplies ? SupplyGroup.OfficeSupplies : SupplyGroup.ItSupplies;
        var inventory = await _db.InventoryItems
            .AsNoTracking()
            .Where(i => i.SupplyGroup == group)
            .OrderBy(i => i.ItemName)
            .ToListAsync(cancellationToken);
        var unavailableByItemId = await InventoryAvailability.GetUnavailableQuantitiesAsync(
            _db,
            inventory.Select(i => i.Id).ToList(),
            cancellationToken);
        var items = inventory
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
                unit = i.Unit.GetDisplayName(),
                availableQuantity = Math.Max(0, i.Quantity - unavailableByItemId.GetValueOrDefault(i.Id))
            })
            .ToList();

        return Json(items);
    }

    private async Task<List<RequisitionRecordItem>> BuildRecordItemsAsync(
        IReadOnlyList<RequisitionLineItemViewModel> lines,
        CancellationToken cancellationToken)
    {
        var itemIds = lines.Select(i => i.InventoryItemId).Distinct().ToList();
        var inventoryById = await _db.InventoryItems
            .AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);
        var recordItems = new List<RequisitionRecordItem>();

        foreach (var line in lines)
        {
            if (!inventoryById.TryGetValue(line.InventoryItemId, out var inventoryItem))
                continue;

            if (inventoryItem.IsSerialized)
            {
                foreach (var serial in RequisitionInventoryStock.ParseSerialNumbers(line.SerialNo))
                    recordItems.Add(BuildRecordItem(line, serial, 1));
            }
            else
            {
                recordItems.Add(BuildRecordItem(line, null, line.Qty));
            }
        }

        return recordItems;

        static RequisitionRecordItem BuildRecordItem(RequisitionLineItemViewModel line, string? serialNo, int qty) => new()
        {
            InventoryItemId = line.InventoryItemId,
            SerialNo = serialNo,
            Qty = qty,
            Unit = line.Unit.Trim(),
            Purpose = line.Purpose.Trim(),
            RfNo = string.IsNullOrWhiteSpace(line.RfNo) ? null : line.RfNo.Trim()
        };
    }

    [HttpGet]
    public async Task<IActionResult> InventoryItemSerials(int id, CancellationToken cancellationToken)
    {
        var unavailableSerials = await _db.RequisitionRecordItems
            .AsNoTracking()
            .Where(i => i.InventoryItemId == id
                && i.SerialNo != null
                && i.RequisitionRecord != null
                && (i.RequisitionRecord.Status == RequisitionStatus.InQueue
                    || i.RequisitionRecord.Status == RequisitionStatus.Pending
                    || i.RequisitionRecord.Status == RequisitionStatus.Approved))
            .Select(i => i.SerialNo!)
            .ToListAsync(cancellationToken);
        var unavailableSet = unavailableSerials
            .SelectMany(RequisitionInventoryStock.ParseSerialNumbers)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var serials = await _db.InventoryItemSerials
            .AsNoTracking()
            .Where(s => s.InventoryItemId == id)
            .OrderBy(s => s.SerialNumber)
            .Select(s => s.SerialNumber)
            .ToListAsync(cancellationToken);

        return Json(serials.Where(s => !unavailableSet.Contains(s)).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkReceived(int id, CancellationToken cancellationToken)
    {
        var requisition = await _db.RequisitionRecords
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (requisition is null)
            return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(currentUserId)
            || !string.Equals(requisition.RequestorUserId, currentUserId, StringComparison.Ordinal))
            return Forbid();

        if (requisition.Status != RequisitionStatus.Approved || requisition.MarkedInUseAt is null)
        {
            TempData["ErrorMessage"] = "Items must be issued before they can be marked as received.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (requisition.ReceivedAt is not null)
        {
            TempData["ErrorMessage"] = "Items have already been marked as received.";
            return RedirectToAction(nameof(Details), new { id });
        }

        requisition.ReceivedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = "Items marked as received.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    [Authorize(Policy = SmrsPolicies.RequisitionApproval)]
    public async Task<IActionResult> Rejected(CancellationToken cancellationToken)
    {
        var rows = await _db.RequisitionRecords
            .AsNoTracking()
            .Include(r => r.Items)
            .Where(r => r.Status == RequisitionStatus.Rejected)
            .ApplyApproverQueueFilter(User)
            .OrderByDescending(r => r.CreatedAt)
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
                RejectionReason = r.RejectionReason,
                MarkedInUseAt = r.MarkedInUseAt
            })
            .ToListAsync(cancellationToken);

        return View(rows);
    }
}
