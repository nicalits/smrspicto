using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PICTO.SMRS.Web.Models.Inventory;

public class AddInventoryItemViewModel
{
    [Required(ErrorMessage = "Item name is required.")]
    [StringLength(256)]
    [Display(Name = "Item name")]
    public string ItemName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Brand is required.")]
    [StringLength(128)]
    public string Brand { get; set; } = string.Empty;

    [Required(ErrorMessage = "Supply group is required.")]
    [Display(Name = "Supply group")]
    public SupplyGroup SupplyGroup { get; set; }

    [Required(ErrorMessage = "Unit is required.")]
    public InventoryUnit Unit { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Quantity must be zero or greater.")]
    [Display(Name = "Quantity on hand")]
    public int Quantity { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "Unit price must be zero or greater.")]
    [Display(Name = "Unit price")]
    [DataType(DataType.Currency)]
    public decimal UnitPrice { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Low stock level must be zero or greater.")]
    [Display(Name = "Low stock level")]
    public int LowStockLevel { get; set; }

    [Required(ErrorMessage = "Location is required.")]
    [StringLength(256)]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(1_000_000)]
    [Display(Name = "Description & specifications")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Serialized item (track serial numbers)")]
    public bool IsSerialized { get; set; }

    /// <summary>One serial number per line; line count must match quantity when serialized.</summary>
    [Display(Name = "Serial numbers (one per line)")]
    public string? SerialNumbersRaw { get; set; }

    [Display(Name = "Item image")]
    public IFormFile? Image { get; set; }
}
