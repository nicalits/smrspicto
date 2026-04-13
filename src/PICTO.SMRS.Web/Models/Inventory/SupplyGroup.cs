using System.ComponentModel.DataAnnotations;

namespace PICTO.SMRS.Web.Models.Inventory;

public enum SupplyGroup
{
    [Display(Name = "IT Supplies")]
    ItSupplies = 0,

    [Display(Name = "Office Supplies")]
    OfficeSupplies = 1
}
