using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Models.Inventory;
using PICTO.SMRS.Web.Security;
using PICTO.SMRS.Web.Services;

namespace PICTO.SMRS.Web.Controllers;

[Authorize(Policy = SmrsPolicies.InventoryAccess)]
public class InventoryController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private static readonly char[] SearchTokenSeparators = [' ', '\t', ',', ';'];

    private const long MaxImageBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp"
    };

    public InventoryController(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? column, string? sort, string? dir, CancellationToken cancellationToken)
    {
        var query = _db.InventoryItems.AsNoTracking().AsQueryable();
        query = ApplySearchFilter(query, q, column);
        var sortBy = NormalizeSortBy(sort);
        var sortDir = NormalizeSortDirection(dir);
        var list = await query.ToListAsync(cancellationToken);
        var unavailableByItemId = await InventoryAvailability.GetUnavailableQuantitiesAsync(
            _db,
            list.Select(i => i.Id).ToList(),
            cancellationToken);
        var rows = ApplySort(
            list.Select(i => MapToRow(i, unavailableByItemId.GetValueOrDefault(i.Id))),
            sortBy,
            sortDir).ToList();

        return View(new InventoryIndexViewModel
        {
            SearchQuery = q ?? string.Empty,
            SearchColumn = string.IsNullOrWhiteSpace(column) ? "all" : column.Trim().ToLowerInvariant(),
            ColumnOptions = BuildColumnOptions(string.IsNullOrWhiteSpace(column) ? "all" : column.Trim().ToLowerInvariant()),
            SortBy = sortBy,
            SortDirection = sortDir,
            Items = rows
        });
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new AddInventoryItemViewModel
        {
            Quantity = 0,
            UnitPrice = 0,
            LowStockLevel = 0
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AddInventoryItemViewModel model, CancellationToken cancellationToken)
    {
        if (model.IsSerialized)
        {
            var serials = ParseSerialLines(model.SerialNumbersRaw);
            if (serials.Count != model.Quantity)
            {
                ModelState.AddModelError(nameof(model.SerialNumbersRaw),
                    model.Quantity == 0
                        ? "Quantity is zero; turn off serialized or enter a quantity that matches the number of serial lines."
                        : $"Enter exactly {model.Quantity} serial number line(s) (one per line). You entered {serials.Count}.");
            }
            else if (serials.Count != serials.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                ModelState.AddModelError(nameof(model.SerialNumbersRaw), "Serial numbers must be unique.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.SerialNumbersRaw))
        {
            ModelState.AddModelError(nameof(model.SerialNumbersRaw), "Clear serial numbers or enable serialized item.");
        }

        if (model.Image is { Length: > 0 })
        {
            if (model.Image.Length > MaxImageBytes)
                ModelState.AddModelError(nameof(model.Image), $"Image must be at most {MaxImageBytes / (1024 * 1024)} MB.");
            else
            {
                var ext = Path.GetExtension(model.Image.FileName);
                if (string.IsNullOrEmpty(ext) || !AllowedImageExtensions.Contains(ext))
                    ModelState.AddModelError(nameof(model.Image), "Allowed image types: JPG, PNG, GIF, WebP.");
            }
        }

        if (!ModelState.IsValid)
            return View(model);

        string? imagePath = null;
        if (model.Image is { Length: > 0 })
            imagePath = await SaveImageAsync(model.Image, cancellationToken);

        var entity = new InventoryItem
        {
            ItemName = model.ItemName.Trim(),
            Brand = string.IsNullOrWhiteSpace(model.Brand) ? null : model.Brand.Trim(),
            SupplyGroup = model.SupplyGroup,
            Unit = model.Unit,
            Quantity = model.Quantity,
            UnitPrice = model.UnitPrice,
            LowStockLevel = model.LowStockLevel,
            Location = string.IsNullOrWhiteSpace(model.Location) ? null : model.Location.Trim(),
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            Specifications = null,
            IsSerialized = model.IsSerialized,
            ImagePath = imagePath,
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (model.IsSerialized)
        {
            foreach (var sn in ParseSerialLines(model.SerialNumbersRaw))
                entity.Serials.Add(new InventoryItemSerial { SerialNumber = sn });
        }

        _db.InventoryItems.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"Item “{entity.ItemName}” was added to inventory.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.InventoryItems
            .Include(i => i.Serials)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (entity is null)
            return NotFound();

        var vm = new AddInventoryItemViewModel
        {
            ItemName = entity.ItemName,
            Brand = entity.Brand,
            SupplyGroup = entity.SupplyGroup,
            Unit = entity.Unit,
            Quantity = entity.Quantity,
            UnitPrice = entity.UnitPrice,
            LowStockLevel = entity.LowStockLevel,
            Location = entity.Location,
            Description = entity.Description,
            IsSerialized = entity.IsSerialized,
            SerialNumbersRaw = entity.IsSerialized
                ? string.Join(Environment.NewLine, entity.Serials.Select(s => s.SerialNumber).OrderBy(x => x))
                : null
        };

        ViewData["EditMode"] = true;
        ViewData["ItemId"] = id;
        return View("Create", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AddInventoryItemViewModel model, CancellationToken cancellationToken)
    {
        var entity = await _db.InventoryItems
            .Include(i => i.Serials)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (entity is null)
            return NotFound();

        if (model.IsSerialized)
        {
            var serials = ParseSerialLines(model.SerialNumbersRaw);
            if (serials.Count != model.Quantity)
            {
                ModelState.AddModelError(nameof(model.SerialNumbersRaw),
                    model.Quantity == 0
                        ? "Quantity is zero; turn off serialized or enter a quantity that matches the number of serial lines."
                        : $"Enter exactly {model.Quantity} serial number line(s) (one per line). You entered {serials.Count}.");
            }
            else if (serials.Count != serials.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                ModelState.AddModelError(nameof(model.SerialNumbersRaw), "Serial numbers must be unique.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.SerialNumbersRaw))
        {
            ModelState.AddModelError(nameof(model.SerialNumbersRaw), "Clear serial numbers or enable serialized item.");
        }

        if (model.Image is { Length: > 0 })
        {
            if (model.Image.Length > MaxImageBytes)
                ModelState.AddModelError(nameof(model.Image), $"Image must be at most {MaxImageBytes / (1024 * 1024)} MB.");
            else
            {
                var ext = Path.GetExtension(model.Image.FileName);
                if (string.IsNullOrEmpty(ext) || !AllowedImageExtensions.Contains(ext))
                    ModelState.AddModelError(nameof(model.Image), "Allowed image types: JPG, PNG, GIF, WebP.");
            }
        }

        if (!ModelState.IsValid)
        {
            ViewData["EditMode"] = true;
            ViewData["ItemId"] = id;
            return View("Create", model);
        }

        if (model.Image is { Length: > 0 })
            entity.ImagePath = await SaveImageAsync(model.Image, cancellationToken);

        entity.ItemName = model.ItemName.Trim();
        entity.Brand = string.IsNullOrWhiteSpace(model.Brand) ? null : model.Brand.Trim();
        entity.SupplyGroup = model.SupplyGroup;
        entity.Unit = model.Unit;
        entity.Quantity = model.Quantity;
        entity.UnitPrice = model.UnitPrice;
        entity.LowStockLevel = model.LowStockLevel;
        entity.Location = string.IsNullOrWhiteSpace(model.Location) ? null : model.Location.Trim();
        entity.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        entity.IsSerialized = model.IsSerialized;

        entity.Serials.Clear();
        if (model.IsSerialized)
        {
            foreach (var sn in ParseSerialLines(model.SerialNumbersRaw))
                entity.Serials.Add(new InventoryItemSerial { SerialNumber = sn });
        }

        await _db.SaveChangesAsync(cancellationToken);
        TempData["StatusMessage"] = $"Item “{entity.ItemName}” was updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (entity is null)
            return NotFound();

        var itemName = entity.ItemName;
        _db.InventoryItems.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        TempData["StatusMessage"] = $"Item “{itemName}” was deleted.";
        return RedirectToAction(nameof(Index));
    }

    private static List<string> ParseSerialLines(string? raw) =>
        (raw ?? string.Empty)
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

    private async Task<string> SaveImageAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var ext = Path.GetExtension(file.FileName);
        var uploads = Path.Combine(_env.WebRootPath, "uploads", "inventory");
        Directory.CreateDirectory(uploads);
        var name = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var physical = Path.Combine(uploads, name);
        await using (var stream = System.IO.File.Create(physical))
            await file.CopyToAsync(stream, cancellationToken);

        return $"uploads/inventory/{name}";
    }

    private static string? CombinedDetailForDisplay(InventoryItem i)
    {
        var d = i.Description?.Trim();
        var s = i.Specifications?.Trim();
        if (string.IsNullOrEmpty(d)) return string.IsNullOrEmpty(s) ? null : s;
        if (string.IsNullOrEmpty(s)) return d;
        return $"{d} · {s}";
    }

    private static InventoryListRowViewModel MapToRow(InventoryItem i, int unavailableQuantity)
    {
        var available = Math.Max(0, i.Quantity - unavailableQuantity);
        var low = i.LowStockLevel > 0 && available <= i.LowStockLevel;
        return new InventoryListRowViewModel
        {
            Id = i.Id,
            ItemName = i.ItemName,
            Brand = i.Brand,
            SupplyGroupDisplay = i.SupplyGroup.GetDisplayName(),
            UnitDisplay = i.Unit.GetDisplayName(),
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            TotalAmount = i.UnitPrice * i.Quantity,
            AvailableQuantity = available,
            LowStockLevel = i.LowStockLevel,
            Location = i.Location,
            Description = CombinedDetailForDisplay(i),
            IsSerialized = i.IsSerialized,
            ImagePath = i.ImagePath,
            IsLowStock = low
        };
    }

    private static IReadOnlyList<SelectListItem> BuildColumnOptions(string selected)
    {
        var items = new (string Value, string Text)[]
        {
            ("all", "All searchable fields"),
            ("itemname", "Item name"),
            ("brand", "Brand"),
            ("supplygroup", "Supply group"),
            ("unit", "Unit"),
            ("description", "Description & specifications"),
            ("location", "Location")
        };
        return items.Select(x => new SelectListItem
        {
            Value = x.Value,
            Text = x.Text,
            Selected = string.Equals(x.Value, selected, StringComparison.OrdinalIgnoreCase)
        }).ToList();
    }

    private static IQueryable<InventoryItem> ApplySearchFilter(IQueryable<InventoryItem> query, string? q, string? column)
    {
        var term = q?.Trim();
        if (string.IsNullOrEmpty(term)) return query;

        var col = string.IsNullOrWhiteSpace(column) ? "all" : column.Trim().ToLowerInvariant();

        return col switch
        {
            "itemname" => query.Where(i => i.ItemName.Contains(term)),
            "brand" => query.Where(i => i.Brand != null && i.Brand.Contains(term)),
            "description" => query.Where(i =>
                (i.Description != null && i.Description.Contains(term))
                || (i.Specifications != null && i.Specifications.Contains(term))),
            "specifications" => query.Where(i =>
                (i.Description != null && i.Description.Contains(term))
                || (i.Specifications != null && i.Specifications.Contains(term))),
            "location" => query.Where(i => i.Location != null && i.Location.Contains(term)),
            "supplygroup" => FilterSupplyGroupColumn(query, term),
            "unit" => FilterUnitColumn(query, term),
            _ => ApplyAllSearchTokens(query, term)
        };
    }

    private static IQueryable<InventoryItem> ApplyAllSearchTokens(IQueryable<InventoryItem> query, string term)
    {
        var tokens = term.Split(SearchTokenSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return query;
        foreach (var raw in tokens)
        {
            var tok = raw.Trim();
            if (tok.Length == 0) continue;
            query = query.Where(i =>
                i.ItemName.Contains(tok)
                || (i.Brand != null && i.Brand.Contains(tok))
                || (i.Description != null && i.Description.Contains(tok))
                || (i.Specifications != null && i.Specifications.Contains(tok))
                || (i.Location != null && i.Location.Contains(tok))
                || i.Serials.Any(s => s.SerialNumber.Contains(tok)));
        }

        return query;
    }

    private static IQueryable<InventoryItem> FilterSupplyGroupColumn(IQueryable<InventoryItem> query, string term)
    {
        var tl = term.Trim().ToLowerInvariant();
        if (tl.Contains("office"))
            return query.Where(i => i.SupplyGroup == SupplyGroup.OfficeSupplies);
        if (tl is "it" || tl.StartsWith("it ") || tl.Contains("it supplies") || tl.Contains("information"))
            return query.Where(i => i.SupplyGroup == SupplyGroup.ItSupplies);
        return query.Where(_ => false);
    }

    private static IQueryable<InventoryItem> FilterUnitColumn(IQueryable<InventoryItem> query, string term)
    {
        var tl = term.Trim().ToLowerInvariant();
        var matched = new List<InventoryUnit>();
        foreach (InventoryUnit u in Enum.GetValues<InventoryUnit>())
        {
            var label = u.GetDisplayName().ToLowerInvariant();
            var compact = label.Replace(" ", "", StringComparison.Ordinal).Replace("/", "", StringComparison.Ordinal);
            if (label.Contains(tl, StringComparison.Ordinal) || tl.Contains(compact, StringComparison.Ordinal))
                matched.Add(u);
        }

        if (matched.Count == 0)
            return query.Where(_ => false);

        return query.Where(i => matched.Contains(i.Unit));
    }

    private static string NormalizeSortBy(string? sort)
    {
        var s = (sort ?? "item").Trim().ToLowerInvariant();
        return s switch
        {
            "item" or "brand" or "group" or "unit" or "qty" or "price" or "total" or "available" or "lowstock" or "location" or "description" or "serialized" => s,
            _ => "item"
        };
    }

    private static string NormalizeSortDirection(string? dir)
        => string.Equals(dir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc";

    private static IEnumerable<InventoryListRowViewModel> ApplySort(
        IEnumerable<InventoryListRowViewModel> query,
        string sortBy,
        string sortDir)
    {
        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        return (sortBy, desc) switch
        {
            ("item", false) => query.OrderBy(i => i.ItemName).ThenBy(i => i.Id),
            ("item", true) => query.OrderByDescending(i => i.ItemName).ThenByDescending(i => i.Id),
            ("brand", false) => query.OrderBy(i => i.Brand).ThenBy(i => i.ItemName),
            ("brand", true) => query.OrderByDescending(i => i.Brand).ThenByDescending(i => i.ItemName),
            ("group", false) => query.OrderBy(i => i.SupplyGroupDisplay).ThenBy(i => i.ItemName),
            ("group", true) => query.OrderByDescending(i => i.SupplyGroupDisplay).ThenByDescending(i => i.ItemName),
            ("unit", false) => query.OrderBy(i => i.UnitDisplay).ThenBy(i => i.ItemName),
            ("unit", true) => query.OrderByDescending(i => i.UnitDisplay).ThenByDescending(i => i.ItemName),
            ("qty", false) => query.OrderBy(i => i.Quantity).ThenBy(i => i.ItemName),
            ("qty", true) => query.OrderByDescending(i => i.Quantity).ThenByDescending(i => i.ItemName),
            ("price", false) => query.OrderBy(i => i.UnitPrice).ThenBy(i => i.ItemName),
            ("price", true) => query.OrderByDescending(i => i.UnitPrice).ThenByDescending(i => i.ItemName),
            ("total", false) => query.OrderBy(i => i.TotalAmount).ThenBy(i => i.ItemName),
            ("total", true) => query.OrderByDescending(i => i.TotalAmount).ThenByDescending(i => i.ItemName),
            ("available", false) => query.OrderBy(i => i.AvailableQuantity).ThenBy(i => i.ItemName),
            ("available", true) => query.OrderByDescending(i => i.AvailableQuantity).ThenByDescending(i => i.ItemName),
            ("lowstock", false) => query.OrderBy(i => i.LowStockLevel).ThenBy(i => i.ItemName),
            ("lowstock", true) => query.OrderByDescending(i => i.LowStockLevel).ThenByDescending(i => i.ItemName),
            ("location", false) => query.OrderBy(i => i.Location).ThenBy(i => i.ItemName),
            ("location", true) => query.OrderByDescending(i => i.Location).ThenByDescending(i => i.ItemName),
            ("description", false) => query.OrderBy(i => i.Description).ThenBy(i => i.ItemName),
            ("description", true) => query.OrderByDescending(i => i.Description).ThenByDescending(i => i.ItemName),
            ("serialized", false) => query.OrderBy(i => i.IsSerialized).ThenBy(i => i.ItemName),
            ("serialized", true) => query.OrderByDescending(i => i.IsSerialized).ThenByDescending(i => i.ItemName),
            _ => query.OrderBy(i => i.ItemName).ThenBy(i => i.Id)
        };
    }
}
