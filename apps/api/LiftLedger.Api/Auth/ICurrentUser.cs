using LiftLedger.Api.Domain;

namespace LiftLedger.Api.Auth;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    Guid TenantId { get; }
    MembershipRole Role { get; }
    string Email { get; }
    string FullName { get; }

    bool CanWrite => IsAuthenticated && Role is MembershipRole.Owner or MembershipRole.Admin or MembershipRole.Examiner;
    bool CanManage => IsAuthenticated && Role is MembershipRole.Owner or MembershipRole.Admin;
}

public static class CurrentUserExtensions
{
    public static void EnsureWriteAccess(this ICurrentUser user)
    {
        if (!user.CanWrite)
        {
            throw new UnauthorizedAccessException("Your role is view-only for this organisation.");
        }
    }

    public static void EnsureManageAccess(this ICurrentUser user)
    {
        if (!user.CanManage)
        {
            throw new UnauthorizedAccessException("Only owners and admins can manage this record.");
        }
    }

    public static void EnsureTenant(this ICurrentUser user, ITenantEntity entity)
    {
        if (!user.IsAuthenticated || entity.TenantId != user.TenantId)
        {
            throw new KeyNotFoundException("The requested record was not found.");
        }
    }

    public static void EnsureTenant(this ICurrentUser user, Guid tenantId)
    {
        if (!user.IsAuthenticated || tenantId != user.TenantId)
        {
            throw new KeyNotFoundException("The requested record was not found.");
        }
    }
}
