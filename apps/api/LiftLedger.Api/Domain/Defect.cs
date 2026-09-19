namespace LiftLedger.Api.Domain;

public class Defect : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid InspectionId { get; set; }
    public Guid AssetId { get; set; }

    public string Description { get; set; } = string.Empty;
    public DefectSeverity Severity { get; set; } = DefectSeverity.Defect;
    public string? Category { get; set; }
    public bool RequiresImmediateWithdrawal { get; set; }
    public DateTime? RectifiedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Inspection Inspection { get; set; } = null!;
    public Asset Asset { get; set; } = null!;
}
