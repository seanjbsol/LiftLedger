namespace LiftLedger.Api.Domain;

public class Defect : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid InspectionId { get; set; }
    public Guid AssetId { get; set; }

    public string Description { get; set; } = string.Empty;
    public DefectSeverity Severity { get; set; } = DefectSeverity.Defect;
    public DefectStatus Status { get; set; } = DefectStatus.Open;
    public string? Category { get; set; }
    public bool RequiresImmediateWithdrawal { get; set; }

    public Guid? AssignedToUserId { get; set; }
    public DateTime? AssignedAt { get; set; }

    public DateTime? RectifiedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public string? CloseNotes { get; set; }
    public bool RequiresRetest { get; set; }
    public Guid? RetestInspectionId { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Inspection Inspection { get; set; } = null!;
    public Asset Asset { get; set; } = null!;
    public User? AssignedTo { get; set; }
    public User? ClosedBy { get; set; }
    public Inspection? RetestInspection { get; set; }
    public ICollection<DefectPhoto> Photos { get; set; } = new List<DefectPhoto>();
}
