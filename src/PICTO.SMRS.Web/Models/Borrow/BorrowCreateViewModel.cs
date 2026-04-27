using System.ComponentModel.DataAnnotations;

namespace PICTO.SMRS.Web.Models.Borrow;

public class BorrowLineItemViewModel
{
    public int? InventoryItemId { get; set; }

    [Required]
    [Display(Name = "Item Description")]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Range(1, 999999)]
    [Display(Name = "Qty")]
    public int Qty { get; set; } = 1;

    [Required]
    [Display(Name = "Location/Venue")]
    [StringLength(300)]
    public string LocationVenue { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Purpose")]
    [StringLength(300)]
    public string Purpose { get; set; } = string.Empty;

    [Display(Name = "Borrow date")]
    public DateOnly? BorrowDate { get; set; }

    [Display(Name = "Borrow time")]
    [StringLength(20)]
    public string? BorrowTime { get; set; }

    [Display(Name = "Return date")]
    public DateOnly? ReturnDate { get; set; }

    [Display(Name = "Return time")]
    [StringLength(20)]
    public string? ReturnTime { get; set; }
}

public class BorrowCreateViewModel
{
    [Display(Name = "RF No.")]
    [StringLength(7)]
    [RegularExpression(@"^$|^\d{2}-\d{4}$", ErrorMessage = "RF No. must be in the format 00-0000.")]
    public string? RfNo { get; set; }

    [Display(Name = "Date")]
    public DateOnly SlipDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Display(Name = "Borrower's name")]
    public string BorrowerName { get; set; } = string.Empty;

    [Display(Name = "Office")]
    [StringLength(200)]
    public string? Office { get; set; }

    [Display(Name = "Division")]
    public string BorrowerDivision { get; set; } = string.Empty;

    [Display(Name = "Time")]
    [StringLength(20)]
    public string? SlipTime { get; set; }

    [Display(Name = "Tel No.")]
    [StringLength(50)]
    public string? TelNo { get; set; }

    [Display(Name = "Remark(s)")]
    [StringLength(2000)]
    public string? Remarks { get; set; }

    [MinLength(1, ErrorMessage = "Add at least one item.")]
    public List<BorrowLineItemViewModel> Items { get; set; } = [new BorrowLineItemViewModel()];
}
