using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Services;

namespace PICTO.SMRS.Web.ViewComponents;

public class PendingApprovalBadgeViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;

    public PendingApprovalBadgeViewComponent(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IViewComponentResult> InvokeAsync(CancellationToken cancellationToken)
    {
        if (User is not ClaimsPrincipal claimsUser || claimsUser.Identity?.IsAuthenticated != true)
            return Content(string.Empty);

        var count = await RequisitionApproverScope.CountPendingForApproverAsync(_db, claimsUser, cancellationToken);
        return View(count);
    }
}
