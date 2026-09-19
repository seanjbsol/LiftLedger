using System.ComponentModel.DataAnnotations;
using LiftLedger.Api.Domain;

namespace LiftLedger.Api.Contracts;

public record AssetSummary(
    Guid Id,
    string AssetNumber,
    string Name,
    AssetCategory Category,
    string? IdentificationCode,
    string? SafeWorkingLoad,
    DateOnly? NextExaminationDue,
    bool IsArchived,
    string? ClientName,
    string? SiteName,
    string DueStatus);

public record AssetDetail(
    Guid Id,
    Guid TenantId,
    string AssetNumber,
    string Name,
    AssetCategory Category,
    string? Make,
    string? Model,
    string? SerialNumber,
    string? IdentificationCode,
    string? SafeWorkingLoad,
    DateOnly? FirstUseDate,
    DateOnly? NextExaminationDue,
    string? Notes,
    bool IsArchived,
    Guid? ClientId,
    Guid? SiteId,
    string? ClientName,
    string? SiteName,
    string DueStatus,
    IReadOnlyList<InspectionSummary> RecentInspections);

public record UpsertAssetRequest
{
    [Required, MaxLength(64)]
    public string AssetNumber { get; init; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public AssetCategory Category { get; init; }

    [MaxLength(100)]
    public string? Make { get; init; }

    [MaxLength(100)]
    public string? Model { get; init; }

    [MaxLength(100)]
    public string? SerialNumber { get; init; }

    [MaxLength(128)]
    public string? IdentificationCode { get; init; }

    [MaxLength(64)]
    public string? SafeWorkingLoad { get; init; }

    public DateOnly? FirstUseDate { get; init; }
    public DateOnly? NextExaminationDue { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }

    public Guid? ClientId { get; init; }
    public Guid? SiteId { get; init; }
}
