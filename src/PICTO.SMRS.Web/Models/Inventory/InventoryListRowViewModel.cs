namespace PICTO.SMRS.Web.Models.Inventory;

public class InventoryListRowViewModel
{
    public int Id { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string SupplyGroupDisplay { get; set; } = string.Empty;
    public string UnitDisplay { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public int AvailableQuantity { get; set; }
    public int LowStockLevel { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public bool IsSerialized { get; set; }
    public string? ImagePath { get; set; }
    public bool IsLowStock { get; set; }
}
