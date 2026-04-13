using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PICTO.SMRS.Web.Controllers;

[Authorize]
public class BorrowController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
