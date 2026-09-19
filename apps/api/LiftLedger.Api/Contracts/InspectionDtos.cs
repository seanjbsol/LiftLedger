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
    string ExaminerName,
    Guid? StoredCertificateId = null);

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
    DateOnly? DateOfLastExamination,
    DateOnly? DateOfReport,
    string? SafeWorkingLoad,
    string? ParticularsOfExamination,
    string? ParticularsOfTests,
    string? ParticularsOfDefects,
    string? RepairsRequired,
    string? ExaminerQualifications,
    string? EmployerName,
    string? EmployerAddress,
    string? PremisesAddress,
    string? CompetentPersonAddress,
    string? AuthenticatingPerson,
    string? Declaration,
    string? CertificateNumber,
    string? PuwerTemplateCode,
    IReadOnlyList<PuwerAnswerDto> PuwerAnswers,
    Guid? StoredCertificateId,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    IReadOnlyList<DefectDto> Defects);

public record DefectPhotoDto(
    Guid Id,
    DefectPhotoKind Kind,
    string FileName,
    string ContentType,
    DateTime UploadedAt);

public record DefectDto(
    Guid Id,
    Guid InspectionId,
    Guid AssetId,
    string AssetNumber,
    string AssetName,
    string Description,
    DefectSeverity Severity,
    DefectStatus Status,
    string? Category,
    bool RequiresImmediateWithdrawal,
    Guid? AssignedToUserId,
    string? AssignedToName,
    DateTime? AssignedAt,
    DateTime? RectifiedAt,
    DateTime? ClosedAt,
    string? CloseNotes,
    bool RequiresRetest,
    Guid? RetestInspectionId,
    string? Notes,
    DateTime CreatedAt,
    IReadOnlyList<DefectPhotoDto> Photos);

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

public record RaiseDefectRequest : DefectInput;

public record AssignDefectRequest
{
    [Required]
    public Guid AssignedToUserId { get; init; }
}

public record CloseDefectRequest
{
    [MaxLength(2000)]
    public string? CloseNotes { get; init; }

    public bool RequiresRetest { get; init; }
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

    public DateOnly? DateOfLastExamination { get; init; }

    public DateOnly? DateOfReport { get; init; }

    [MaxLength(64)]
    public string? SafeWorkingLoad { get; init; }

    [MaxLength(4000)]
    public string? ParticularsOfExamination { get; init; }

    [MaxLength(4000)]
    public string? ParticularsOfTests { get; init; }

    [MaxLength(4000)]
    public string? ParticularsOfDefects { get; init; }

    [MaxLength(4000)]
    public string? RepairsRequired { get; init; }

    [MaxLength(200)]
    public string? ExaminerQualifications { get; init; }

    [MaxLength(200)]
    public string? EmployerName { get; init; }

    [MaxLength(500)]
    public string? EmployerAddress { get; init; }

    [MaxLength(500)]
    public string? PremisesAddress { get; init; }

    [MaxLength(500)]
    public string? CompetentPersonAddress { get; init; }

    [MaxLength(200)]
    public string? AuthenticatingPerson { get; init; }

    [MaxLength(2000)]
    public string? Declaration { get; init; }

    [MaxLength(64)]
    public string? PuwerTemplateCode { get; init; }

    public List<PuwerAnswerDto> PuwerAnswers { get; init; } = new();

    public List<DefectInput> Defects { get; init; } = new();
}

public record PuwerAnswerDto
{
    [Required, MaxLength(64)]
    public string ItemId { get; init; } = string.Empty;

    public PuwerItemResult Result { get; init; } = PuwerItemResult.Suitable;

    [MaxLength(1000)]
    public string? Notes { get; init; }
}

public record PuwerTemplateItemDto(string Id, string Section, string Prompt);

public record PuwerTemplateDto(
    string Code,
    AssetCategory Category,
    string Title,
    string Introduction,
    IReadOnlyList<PuwerTemplateItemDto> Items);

public record CertificateSummary(
    Guid Id,
    Guid InspectionId,
    Guid AssetId,
    Guid? ClientId,
    CertificateKind Kind,
    string CertificateNumber,
    string TemplateCode,
    string AssetNumber,
    string AssetName,
    DateTime IssuedAt);

public record AssetScanResponse(
    Guid Id,
    string AssetNumber,
    string Name,
    AssetCategory Category,
    string? IdentificationCode,
    string DeepLink,
    Guid? LastInspectionId,
    Guid? LastCertificateId,
    string? LastCertificateNumber,
    DateOnly? LastExaminationDate);

public record MemberDto(Guid UserId, string FullName, string Email, MembershipRole Role);

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

public record CreatePortalTokenRequest
{
    [Required]
    public Guid ClientId { get; init; }

    [MaxLength(200)]
    public string? Label { get; init; }

    public int? ValidDays { get; init; }
}

public record PortalTokenResponse(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string Token,
    string PublicUrl,
    DateTime ExpiresAt);

public record PublicPortalCertificate(
    Guid Id,
    string CertificateNumber,
    string AssetNumber,
    string AssetName,
    CertificateKind Kind,
    DateTime IssuedAt);

public record PublicPortalView(
    string OrganisationName,
    string ClientName,
    string Disclaimer,
    IReadOnlyList<PublicPortalCertificate> Certificates);
