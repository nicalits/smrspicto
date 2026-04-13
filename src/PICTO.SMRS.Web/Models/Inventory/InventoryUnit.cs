using System.ComponentModel.DataAnnotations;

namespace PICTO.SMRS.Web.Models.Inventory;

public enum InventoryUnit
{
    Box = 0,
    Can = 1,
    Meter = 2,

    [Display(Name = "Pc / Pcs")]
    Pc = 3,

    Reams = 4,

    [Display(Name = "N/A")]
    Na = 5
}
