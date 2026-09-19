namespace LiftLedger.Api.Domain;

/// <summary>
/// Stored working record (HTML + PDF) issued from a completed examination.
/// Schedule 1 style / PUWER assessment template — not an HSE-certified form.
/// </summary>
public class Certificate : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid InspectionId { get; set; }
    public Guid AssetId { get; set; }
    public Guid? ClientId { get; set; }

    public CertificateKind Kind { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    public string TemplateCode { get; set; } = string.Empty;
    public string Html { get; set; } = string.Empty;
    public byte[] Pdf { get; set; } = [];
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public Inspection Inspection { get; set; } = null!;
    public Asset Asset { get; set; } = null!;
    public Client? Client { get; set; }
}
