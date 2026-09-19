using System.ComponentModel.DataAnnotations;
using LiftLedger.Api.Domain;

namespace LiftLedger.Api.Contracts;

public record RegisterRequest
{
    [Required, MaxLength(200)]
    public string OrganisationName { get; init; } = string.Empty;

    [MaxLength(200)]
    public string? TradingName { get; init; }

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MaxLength(200)]
    public string FullName { get; init; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    [MaxLength(16)]
    public string? Postcode { get; init; }

    [MaxLength(100)]
    public string? Town { get; init; }
}

public record LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public record AuthResponse(
    string Token,
    UserSummary User,
    TenantSummary Tenant,
    string Role);

public record UserSummary(Guid Id, string Email, string FullName);

public record TenantSummary(
    Guid Id,
    string Name,
    string? TradingName,
    string? Town,
    string? Postcode);

public record MeResponse(
    UserSummary User,
    TenantSummary Tenant,
    string Role);
