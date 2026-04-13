namespace PICTO.SMRS.Web.Models.Inventory;

public class InventoryItemSerial
{
    public int Id { get; set; }

    public int InventoryItemId { get; set; }

    public InventoryItem? InventoryItem { get; set; }

    public string SerialNumber { get; set; } = string.Empty;
}
