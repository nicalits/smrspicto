using Microsoft.AspNetCore.Mvc.Rendering;

namespace PICTO.SMRS.Web.Models.Inventory;

public class InventoryIndexViewModel
{
    public string SearchQuery { get; set; } = string.Empty;

    /// <summary>Filter key: all, itemname, brand, supplygroup, unit, description, specifications, location.</summary>
    public string SearchColumn { get; set; } = "all";

    public IReadOnlyList<SelectListItem> ColumnOptions { get; set; } = Array.Empty<SelectListItem>();

    public string SortBy { get; set; } = "item";

    public string SortDirection { get; set; } = "asc";

    public IReadOnlyList<InventoryListRowViewModel> Items { get; set; } = Array.Empty<InventoryListRowViewModel>();
}
