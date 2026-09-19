using System.Security.Claims;
using LiftLedger.Api.Domain;

namespace LiftLedger.Api.Auth;

public class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated =>
        Principal?.Identity?.IsAuthenticated == true
        && TenantId != Guid.Empty
        && UserId != Guid.Empty;

    public Guid UserId => ParseGuid(ClaimTypes.NameIdentifier, "sub");

    public Guid TenantId => ParseGuid("tenant_id");

    public MembershipRole Role
    {
        get
        {
            var value = Find("role") ?? Principal?.FindFirst(ClaimTypes.Role)?.Value;
            return Enum.TryParse<MembershipRole>(value, true, out var role) ? role : MembershipRole.Viewer;
        }
    }

    public string Email => Find("email") ?? Principal?.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;

    public string FullName => Find("name") ?? Principal?.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;

    private Guid ParseGuid(params string[] types)
    {
        foreach (var type in types)
        {
            var raw = Find(type);
            if (Guid.TryParse(raw, out var id))
            {
                return id;
            }
        }

        return Guid.Empty;
    }

    private string? Find(string type) => Principal?.FindFirst(type)?.Value;
}
