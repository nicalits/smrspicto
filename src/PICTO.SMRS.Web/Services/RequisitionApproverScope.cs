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
        if (user.IsInRole(SmrsRoles.Admin))
            return true;
        if (user.IsInRole(SmrsRoles.ItDivisionHead) && itemType == RequisitionItemType.ItSupplies)
            return true;
        if (user.IsInRole(SmrsRoles.OfficeDivisionHead) && itemType == RequisitionItemType.OfficeSupplies)
            return true;
        return false;
    }

    /// <summary>
    /// Returns true when the user can view a requisition in the approval queue,
    /// regardless of whether they can take action. DeptHead can view but not act.
    /// </summary>
    public static bool CanViewItemType(ClaimsPrincipal user, RequisitionItemType itemType)
    {
        if (user.IsInRole(SmrsRoles.Admin) || user.IsInRole(SmrsRoles.DepartmentHead))
            return true;
        if (user.IsInRole(SmrsRoles.ItDivisionHead) && itemType == RequisitionItemType.ItSupplies)
            return true;
        if (user.IsInRole(SmrsRoles.OfficeDivisionHead) && itemType == RequisitionItemType.OfficeSupplies)
            return true;
        return false;
    }

    /// <summary>
    /// Limits the queue by supply type (IT vs Office) according to division-head roles.
    /// <see cref="SmrsRoles.Admin"/> and <see cref="SmrsRoles.DepartmentHead"/> see both types.
    /// </summary>
    public static IQueryable<RequisitionRecord> ApplyApproverQueueFilter(
        this IQueryable<RequisitionRecord> query,
        ClaimsPrincipal user)
    {
        var seesAll = user.IsInRole(SmrsRoles.Admin) || user.IsInRole(SmrsRoles.DepartmentHead);
        if (!seesAll && user.IsInRole(SmrsRoles.ItDivisionHead))
            query = query.Where(r => r.ItemType == RequisitionItemType.ItSupplies);
        else if (!seesAll && user.IsInRole(SmrsRoles.OfficeDivisionHead))
            query = query.Where(r => r.ItemType == RequisitionItemType.OfficeSupplies);

        return query;
    }

    public static Task<int> CountInQueueForApproverAsync(
        ApplicationDbContext db,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default) =>
        db.RequisitionRecords
            .AsNoTracking()
            .ApplyApproverQueueFilter(user)
            .Where(r => r.Status == RequisitionStatus.InQueue || r.Status == RequisitionStatus.Pending)
            .CountAsync(cancellationToken);
}
