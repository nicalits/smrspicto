using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Models.Borrow;

namespace PICTO.SMRS.Web.ViewComponents;

public class BorrowApprovalBadgeViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;

    public BorrowApprovalBadgeViewComponent(ApplicationDbContext db) => _db = db;

    public async Task<IViewComponentResult> InvokeAsync(CancellationToken cancellationToken)
    {
        if (User is not ClaimsPrincipal claimsUser || claimsUser.Identity?.IsAuthenticated != true)
            return Content(string.Empty);

        var count = await _db.BorrowRecords
            .AsNoTracking()
            .CountAsync(r => r.Status == BorrowStatus.InQueue || r.Status == BorrowStatus.Pending, cancellationToken);

        if (count == 0)
            return Content(string.Empty);

        return Content($"<span class=\"badge rounded-pill bg-danger smrs-sidenav-approval-badge\">{count}</span>");
    }
}
