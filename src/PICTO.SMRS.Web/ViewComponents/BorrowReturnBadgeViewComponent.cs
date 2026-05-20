using System.Security.Claims;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Models.Borrow;

namespace PICTO.SMRS.Web.ViewComponents;

public class BorrowReturnBadgeViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;

    public BorrowReturnBadgeViewComponent(ApplicationDbContext db) => _db = db;

    public async Task<IViewComponentResult> InvokeAsync(CancellationToken cancellationToken)
    {
        if (User is not ClaimsPrincipal claimsUser || claimsUser.Identity?.IsAuthenticated != true)
            return Content(string.Empty);

        var count = await _db.BorrowRecords
            .AsNoTracking()
            .CountAsync(r => r.Status == BorrowStatus.Approved
                && r.MarkedReturnedAt != null
                && r.ReturnConfirmedAt == null, cancellationToken);

        if (count == 0)
            return Content(string.Empty);

        return new HtmlContentViewComponentResult(
            new HtmlString($"<span class=\"badge rounded-pill bg-danger smrs-sidenav-approval-badge\">{count}</span>"));
    }
}
