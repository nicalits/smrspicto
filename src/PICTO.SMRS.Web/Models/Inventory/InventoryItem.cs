namespace PICTO.SMRS.Web.Models.Inventory;

public class InventoryItem
{
    public int Id { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public string? Brand { get; set; }

    public SupplyGroup SupplyGroup { get; set; }

    public InventoryUnit Unit { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public int ReservedQuantity { get; set; }

    public int LowStockLevel { get; set; }

    public string? Location { get; set; }

    public string? Description { get; set; }

    /// <summary>Legacy-only; keyword search still reads this for old rows.</summary>
    public string? Specifications { get; set; }

    public bool IsSerialized { get; set; }

    /// <summary>Relative web path, e.g. uploads/inventory/abc.png</summary>
    public string? ImagePath { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<InventoryItemSerial> Serials { get; set; } = new List<InventoryItemSerial>();
}
