using System.ComponentModel.DataAnnotations;

namespace PICTO.SMRS.Web.Models.Requisitions;

public enum RequisitionItemType
{
    ItSupplies = 1,
    OfficeSupplies = 2
}

public class RequisitionLineItemViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Please select an item.")]
    [Display(Name = "Item Name")]
    public int InventoryItemId { get; set; }

    [Display(Name = "Serial No.")]
    [StringLength(200)]
    public string? SerialNo { get; set; }

    [Range(1, 999999)]
    [Display(Name = "Qty")]
    public int Qty { get; set; } = 1;

    [Required]
    [Display(Name = "Unit")]
    [StringLength(50)]
    public string Unit { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Purpose")]
    [StringLength(300)]
    public string Purpose { get; set; } = string.Empty;

    [Display(Name = "RF No.")]
    [StringLength(7)]
    [RegularExpression(@"^$|^\d{2}-\d{4}$", ErrorMessage = "RF No. must be in the format 00-0000.")]
    public string? RfNo { get; set; }
}

public class RequisitionCreateViewModel
{
    [Required]
    [Display(Name = "RS No.")]
    [StringLength(7, MinimumLength = 7, ErrorMessage = "RS No. must be 7 characters (00-0000).")]
    [RegularExpression(@"^\d{2}-\d{4}$", ErrorMessage = "RS No. must be in the format 00-0000.")]
    public string RsNo { get; set; } = string.Empty;

    [Display(Name = "Type of Item")]
    public RequisitionItemType ItemType { get; set; } = RequisitionItemType.ItSupplies;

    [Display(Name = "Date")]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Display(Name = "Name")]
    public string RequestorName { get; set; } = string.Empty;

    [Display(Name = "Position")]
    public string RequestorPosition { get; set; } = string.Empty;

    [Display(Name = "Division")]
    public string RequestorDivision { get; set; } = string.Empty;

    [MinLength(1, ErrorMessage = "Add at least one item.")]
    public List<RequisitionLineItemViewModel> Items { get; set; } = [new RequisitionLineItemViewModel()];

    [Display(Name = "Office")]
    [StringLength(200)]
    public string? Office { get; set; }

    [Display(Name = "Position")]
    [StringLength(200)]
    public string? MrIcsPosition { get; set; }
}

