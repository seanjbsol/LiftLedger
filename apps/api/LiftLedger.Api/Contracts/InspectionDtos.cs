using System.ComponentModel.DataAnnotations;
using LiftLedger.Api.Domain;

namespace LiftLedger.Api.Contracts;

public record InspectionSummary(
    Guid Id,
    Guid AssetId,
    string AssetNumber,
    string AssetName,
    ExaminationType ExaminationType,
    InspectionStatus Status,
    InspectionResult? Result,
    DateOnly ExaminationDate,
    DateOnly? NextDueDate,
    string? CertificateNumber,
    string ExaminerName);

public record InspectionDetail(
    Guid Id,
    Guid TenantId,
    Guid AssetId,
    string AssetNumber,
    string AssetName,
    Guid ExaminerUserId,
    string ExaminerName,
    ExaminationType ExaminationType,
    InspectionStatus Status,
    InspectionResult? Result,
    DateOnly ExaminationDate,
    DateOnly? NextDueDate,
    string? SafeWorkingLoad,
    string? ParticularsOfExamination,
    string? ParticularsOfDefects,
    string? RepairsRequired,
    string? ExaminerQualifications,
    string? CertificateNumber,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    IReadOnlyList<DefectDto> Defects);

public record DefectDto(
    Guid Id,
    string Description,
    DefectSeverity Severity,
    string? Category,
    bool RequiresImmediateWithdrawal,
    DateTime? RectifiedAt,
    string? Notes);

public record DefectInput
{
    [Required, MaxLength(2000)]
    public string Description { get; init; } = string.Empty;

    public DefectSeverity Severity { get; init; } = DefectSeverity.Defect;

    [MaxLength(100)]
    public string? Category { get; init; }

    public bool RequiresImmediateWithdrawal { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public record StartInspectionRequest
{
    [Required]
    public Guid AssetId { get; init; }

    public ExaminationType ExaminationType { get; init; } = ExaminationType.ThoroughExamination;

    public DateOnly? ExaminationDate { get; init; }

    [MaxLength(200)]
    public string? ExaminerQualifications { get; init; }
}

public record CompleteInspectionRequest
{
    [Required]
    public InspectionResult Result { get; init; }

    public DateOnly? ExaminationDate { get; init; }

    [Required]
    public DateOnly NextDueDate { get; init; }

    [MaxLength(64)]
    public string? SafeWorkingLoad { get; init; }

    [MaxLength(4000)]
    public string? ParticularsOfExamination { get; init; }

    [MaxLength(4000)]
    public string? ParticularsOfDefects { get; init; }

    [MaxLength(4000)]
    public string? RepairsRequired { get; init; }

    [MaxLength(200)]
    public string? ExaminerQualifications { get; init; }

    public List<DefectInput> Defects { get; init; } = new();
}

public record DashboardResponse(
    TenantSummary Tenant,
    int AssetCount,
    int OverdueCount,
    int DueSoonCount,
    int OpenDefectCount,
    IReadOnlyList<AssetSummary> Overdue,
    IReadOnlyList<AssetSummary> DueSoon,
    IReadOnlyList<InspectionSummary> RecentInspections);

public record ClientDto(
    Guid Id,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Town,
    string? Postcode);

public record UpsertClientRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(200)]
    public string? ContactName { get; init; }

    [EmailAddress, MaxLength(256)]
    public string? Email { get; init; }

    [MaxLength(32)]
    public string? Phone { get; init; }

    [MaxLength(200)]
    public string? AddressLine1 { get; init; }

    [MaxLength(100)]
    public string? Town { get; init; }

    [MaxLength(16)]
    public string? Postcode { get; init; }
}

public record SiteDto(
    Guid Id,
    Guid? ClientId,
    string? ClientName,
    string Name,
    string? AddressLine1,
    string? Town,
    string? Postcode);

public record UpsertSiteRequest
{
    public Guid? ClientId { get; init; }

    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(200)]
    public string? AddressLine1 { get; init; }

    [MaxLength(100)]
    public string? Town { get; init; }

    [MaxLength(16)]
    public string? Postcode { get; init; }
}
