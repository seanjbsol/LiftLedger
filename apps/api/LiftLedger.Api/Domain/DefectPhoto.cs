namespace LiftLedger.Api.Domain;

public class DefectPhoto : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid DefectId { get; set; }
    public DefectPhotoKind Kind { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/jpeg";
    public byte[] Data { get; set; } = [];
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Defect Defect { get; set; } = null!;
    public User UploadedBy { get; set; } = null!;
}
