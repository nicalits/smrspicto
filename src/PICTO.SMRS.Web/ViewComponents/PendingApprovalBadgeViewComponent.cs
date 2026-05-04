using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Models.Requisitions;
using PICTO.SMRS.Web.Security;
using PICTO.SMRS.Web.Services;

namespace PICTO.SMRS.Web.ViewComponents;

public sealed class PendingApprovalBadgeViewModel
{
    public int TotalCount { get; init; }

    public bool ShowSplitCounts { get; init; }

    public int ItCount { get; init; }

    public int OfficeCount { get; init; }
}

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

        var pendingApprovals = _db.RequisitionRecords
            .AsNoTracking()
            .ApplyApproverQueueFilter(claimsUser)
            .Where(r => r.Status == RequisitionStatus.Pending);
        var showSplitCounts = claimsUser.IsInRole(SmrsRoles.DepartmentHead);

        var model = new PendingApprovalBadgeViewModel
        {
            TotalCount = await pendingApprovals.CountAsync(cancellationToken),
            ShowSplitCounts = showSplitCounts,
            ItCount = showSplitCounts
                ? await pendingApprovals.CountAsync(r => r.ItemType == RequisitionItemType.ItSupplies, cancellationToken)
                : 0,
            OfficeCount = showSplitCounts
                ? await pendingApprovals.CountAsync(r => r.ItemType == RequisitionItemType.OfficeSupplies, cancellationToken)
                : 0
        };

        return View(model);
    }
}
