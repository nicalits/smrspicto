using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Models.Inventory;
using PICTO.SMRS.Web.Models.Borrow;
using PICTO.SMRS.Web.Security;

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

        if (borrow.Status != BorrowStatus.Pending)
        {
            TempData["ErrorMessage"] = "Only pending borrow requests can be edited.";
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
            Status = BorrowStatus.Pending,
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

        if (borrow.Status != BorrowStatus.Pending)
        {
            TempData["ErrorMessage"] = "Only pending borrow requests can be edited.";
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
            .OrderBy(r => r.Status == BorrowStatus.Pending ? 0 : 1)
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
                IssuedAt = r.IssuedAt
            })
            .ToListAsync(cancellationToken);

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
        var canApprove = User.IsInRole(SmrsRoles.Encoder) || User.IsInRole(SmrsRoles.DepartmentHead);

        if (!isRequestOwner && !canApprove)
            return Forbid();

        ViewData["CanTakeAction"] = canApprove && borrow.Status == BorrowStatus.Pending;
        ViewData["CanEdit"] = isRequestOwner && borrow.Status == BorrowStatus.Pending;
        ViewData["BackAction"] = canApprove && !isRequestOwner ? nameof(Approvals) : nameof(Index);

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
    [Authorize(Policy = SmrsPolicies.BorrowApproval)]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        var borrow = await _db.BorrowRecords.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (borrow is null)
            return NotFound();

        if (borrow.Status != BorrowStatus.Pending)
        {
            TempData["ErrorMessage"] = "This borrow request is already processed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var now = DateTimeOffset.UtcNow;
        borrow.Status = BorrowStatus.Approved;
        borrow.ApprovedAt = now;
        borrow.IssuedAt = now;
        borrow.ActionedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = "Borrow request approved and issued.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SmrsPolicies.BorrowApproval)]
    public async Task<IActionResult> Reject(int id, CancellationToken cancellationToken)
    {
        var borrow = await _db.BorrowRecords.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (borrow is null)
            return NotFound();

        if (borrow.Status != BorrowStatus.Pending)
        {
            TempData["ErrorMessage"] = "This borrow request is already processed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        borrow.Status = BorrowStatus.Rejected;
        borrow.RejectedAt = DateTimeOffset.UtcNow;
        borrow.ActionedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = "Borrow request rejected.";
        return RedirectToAction(nameof(Details), new { id });
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
