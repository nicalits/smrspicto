using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Models.Requisitions;
using PICTO.SMRS.Web.Security;

namespace PICTO.SMRS.Web.Services;

public static class RequisitionApproverScope
{
    public static bool CanApproveItemType(ClaimsPrincipal user, RequisitionItemType itemType)
    {
        if (user.IsInRole(SmrsRoles.DepartmentHead))
            return true;
        if (user.IsInRole(SmrsRoles.ItDivisionHead) && itemType == RequisitionItemType.ItSupplies)
            return true;
        if (user.IsInRole(SmrsRoles.OfficeDivisionHead) && itemType == RequisitionItemType.OfficeSupplies)
            return true;
        return false;
    }

    /// <summary>
    /// Limits the queue by supply type (IT vs Office) according to division-head roles.
    /// <see cref="SmrsRoles.DepartmentHead"/> sees both types.
    /// </summary>
    public static IQueryable<RequisitionRecord> ApplyApproverQueueFilter(
        this IQueryable<RequisitionRecord> query,
        ClaimsPrincipal user)
    {
        if (user.IsInRole(SmrsRoles.ItDivisionHead) && !user.IsInRole(SmrsRoles.DepartmentHead))
            query = query.Where(r => r.ItemType == RequisitionItemType.ItSupplies);
        else if (user.IsInRole(SmrsRoles.OfficeDivisionHead) && !user.IsInRole(SmrsRoles.DepartmentHead))
            query = query.Where(r => r.ItemType == RequisitionItemType.OfficeSupplies);

        return query;
    }

    public static Task<int> CountPendingForApproverAsync(
        ApplicationDbContext db,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default) =>
        db.RequisitionRecords
            .AsNoTracking()
            .ApplyApproverQueueFilter(user)
            .Where(r => r.Status == RequisitionStatus.Pending)
            .CountAsync(cancellationToken);
}
