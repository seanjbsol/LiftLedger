using LiftLedger.Api.Auth;
using LiftLedger.Api.Billing;
using LiftLedger.Api.Contracts;
using LiftLedger.Api.Data;
using LiftLedger.Api.Domain;
using LiftLedger.Api.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LiftLedger.Api.Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _tokens;
    private readonly ICurrentUser _currentUser;
    private readonly ISubscriptionClient _subscriptions;
    private readonly SubscriptionApiOptions _subscriptionOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext db,
        JwtTokenService tokens,
        ICurrentUser currentUser,
        ISubscriptionClient subscriptions,
        IOptions<SubscriptionApiOptions> subscriptionOptions,
        ILogger<AuthService> logger)
    {
        _db = db;
        _tokens = tokens;
        _currentUser = currentUser;
        _subscriptions = subscriptions;
        _subscriptionOptions = subscriptionOptions.Value;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = NormaliseEmail(request.Email);
        if (await _db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var tenant = new Tenant
        {
            Name = request.OrganisationName.Trim(),
            TradingName = TrimToNull(request.TradingName),
            Town = TrimToNull(request.Town),
            Postcode = TrimToNull(request.Postcode)?.ToUpperInvariant()
        };

        var user = new User
        {
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        var membership = new Membership
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = MembershipRole.Owner
        };

        _db.Tenants.Add(tenant);
        _db.Users.Add(user);
        _db.Memberships.Add(membership);
        await _db.SaveChangesAsync(cancellationToken);

        await UpsertBillingTenantAsync(tenant, user.Email, cancellationToken);

        return CreateAuthResponse(user, tenant, membership.Role);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormaliseEmail(request.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new AuthenticationFailedException("Email or password is not recognised.");
        }

        var membership = await _db.Memberships
            .IgnoreQueryFilters()
            .Include(m => m.Tenant)
            .Where(m => m.UserId == user.Id && m.Tenant.IsActive)
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (membership is null)
        {
            throw new AuthenticationFailedException("No active organisation membership was found for this account.");
        }

        return CreateAuthResponse(user, membership.Tenant, membership.Role);
    }

    public async Task<MeResponse> GetMeAsync(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Sign in required.");
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == _currentUser.TenantId, cancellationToken)
                     ?? throw new KeyNotFoundException("Organisation was not found.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, cancellationToken)
                   ?? throw new KeyNotFoundException("Account was not found.");

        return new MeResponse(
            new UserSummary(user.Id, user.Email, user.FullName),
            MapTenant(tenant),
            _currentUser.Role.ToString());
    }

    public async Task<IReadOnlyList<MemberDto>> ListMembersAsync(CancellationToken cancellationToken)
    {
        var members = await _db.Memberships.AsNoTracking()
            .Include(m => m.User)
            .OrderBy(m => m.User.FullName)
            .ToListAsync(cancellationToken);
        return members.Select(m => new MemberDto(m.UserId, m.User.FullName, m.User.Email, m.Role)).ToList();
    }

    private AuthResponse CreateAuthResponse(User user, Tenant tenant, MembershipRole role)
    {
        var token = _tokens.CreateToken(user, tenant, role);
        return new AuthResponse(
            token,
            new UserSummary(user.Id, user.Email, user.FullName),
            MapTenant(tenant),
            role.ToString());
    }

    private async Task UpsertBillingTenantAsync(Tenant tenant, string ownerEmail, CancellationToken cancellationToken)
    {
        try
        {
            await _subscriptions.UpsertTenantAsync(
                new UpsertTenantRequest
                {
                    ProductCode = _subscriptionOptions.ProductCode,
                    ExternalTenantId = tenant.Id.ToString(),
                    Name = tenant.Name,
                    OwnerEmail = ownerEmail
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Failed to upsert organisation {TenantId} to the Qck subscription API",
                tenant.Id);
        }
    }

    internal static TenantSummary MapTenant(Tenant tenant) =>
        new(tenant.Id, tenant.Name, tenant.TradingName, tenant.Town, tenant.Postcode);

    private static string NormaliseEmail(string email) => email.Trim().ToLowerInvariant();

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
