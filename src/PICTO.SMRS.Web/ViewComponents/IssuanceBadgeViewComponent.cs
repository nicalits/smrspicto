using System.Security.Claims;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Models.Requisitions;

namespace PICTO.SMRS.Web.ViewComponents;

public class IssuanceBadgeViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;

    public IssuanceBadgeViewComponent(ApplicationDbContext db) => _db = db;

    public async Task<IViewComponentResult> InvokeAsync(CancellationToken cancellationToken)
    {
        if (User is not ClaimsPrincipal claimsUser || claimsUser.Identity?.IsAuthenticated != true)
            return Content(string.Empty);

        var count = await _db.RequisitionRecords
            .AsNoTracking()
            .CountAsync(
                r => r.Status == RequisitionStatus.Approved && r.MarkedInUseAt == null,
                cancellationToken);

        if (count == 0)
            return Content(string.Empty);

        return new HtmlContentViewComponentResult(
            new HtmlString($"<span class=\"badge rounded-pill bg-danger smrs-sidenav-approval-badge\">{count}</span>"));
    }
}
