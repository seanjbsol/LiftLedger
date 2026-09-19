namespace LiftLedger.Api.Domain;

/// <summary>
/// Thorough examination or other inspection. Field names follow LOLER Schedule 1
/// record-keeping (date, examiner, SWL, defects, next due) without claiming
/// that the output is an HSE-certified form.
/// </summary>
public class Inspection : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AssetId { get; set; }
    public Guid ExaminerUserId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? SiteId { get; set; }

    public ExaminationType ExaminationType { get; set; } = ExaminationType.ThoroughExamination;
    public InspectionStatus Status { get; set; } = InspectionStatus.Draft;
    public InspectionResult? Result { get; set; }

    public DateOnly ExaminationDate { get; set; }
    public DateOnly? NextDueDate { get; set; }

    public string? SafeWorkingLoad { get; set; }
    public string? ParticularsOfExamination { get; set; }
    public string? ParticularsOfDefects { get; set; }
    public string? RepairsRequired { get; set; }
    public string? ExaminerQualifications { get; set; }
    public string? CertificateNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Asset Asset { get; set; } = null!;
    public User Examiner { get; set; } = null!;
    public Client? Client { get; set; }
    public Site? Site { get; set; }
    public ICollection<Defect> Defects { get; set; } = new List<Defect>();
}
