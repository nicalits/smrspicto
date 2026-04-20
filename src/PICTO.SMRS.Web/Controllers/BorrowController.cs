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
    public IActionResult Index()
    {
        return View();
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(BorrowCreateViewModel model)
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

        TempData["StatusMessage"] = "Borrow slip submitted. Workflow will record this in a later update.";
        return RedirectToAction(nameof(Index));
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
