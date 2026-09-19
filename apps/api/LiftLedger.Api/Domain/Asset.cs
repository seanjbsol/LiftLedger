namespace LiftLedger.Api.Domain;

public class Asset : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? SiteId { get; set; }

    /// <summary>Workshop / fleet identifier, unique per tenant (e.g. TRI-1042).</summary>
    public string AssetNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AssetCategory Category { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }

    /// <summary>QR payload or stamped plant/trailer code used on labels.</summary>
    public string? IdentificationCode { get; set; }

    /// <summary>Safe working load / rated capacity as marked on the equipment.</summary>
    public string? SafeWorkingLoad { get; set; }

    public DateOnly? FirstUseDate { get; set; }
    public DateOnly? NextExaminationDue { get; set; }
    public string? Notes { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Tenant { get; set; } = null!;
    public Client? Client { get; set; }
    public Site? Site { get; set; }
    public ICollection<Inspection> Inspections { get; set; } = new List<Inspection>();
}
