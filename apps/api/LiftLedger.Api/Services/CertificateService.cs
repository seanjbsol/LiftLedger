using System.Globalization;
using System.Net;
using System.Text.Json;
using LiftLedger.Api.Auth;
using LiftLedger.Api.Billing;
using LiftLedger.Api.Contracts;
using LiftLedger.Api.Data;
using LiftLedger.Api.Domain;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LiftLedger.Api.Services;

public class CertificateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IFeatureGate _features;

    public CertificateService(AppDbContext db, ICurrentUser currentUser, IFeatureGate features)
    {
        _db = db;
        _currentUser = currentUser;
        _features = features;
    }

    public async Task<IReadOnlyList<CertificateSummary>> ListAsync(Guid? assetId, CancellationToken cancellationToken)
    {
        await _features.EnsureCertificatesAsync(cancellationToken);

        var query = _db.Certificates.AsNoTracking().Include(c => c.Asset).AsQueryable();
        if (assetId is not null)
        {
            query = query.Where(c => c.AssetId == assetId);
        }

        var items = await query
            .OrderByDescending(c => c.IssuedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        return items.Select(MapSummary).ToList();
    }

    public async Task<Certificate> GetStoredAsync(Guid id, CancellationToken cancellationToken)
    {
        await _features.EnsureCertificatesAsync(cancellationToken);
        var certificate = await _db.Certificates.Include(c => c.Asset)
                              .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                          ?? throw new KeyNotFoundException("Certificate was not found.");
        _currentUser.EnsureTenant(certificate);
        return certificate;
    }

    public async Task<Certificate> IssueFromInspectionAsync(Guid inspectionId, CancellationToken cancellationToken)
    {
        await _features.EnsureCertificatesAsync(cancellationToken);
        var inspection = await LoadCompletedAsync(inspectionId, cancellationToken);
        return await IssueCoreAsync(inspection, cancellationToken);
    }

    public async Task<Certificate?> TryIssueAsync(Inspection inspection, CancellationToken cancellationToken)
    {
        var plan = await _features.GetAsync(cancellationToken);
        if (!plan.IsPro)
        {
            return null;
        }

        return await IssueCoreAsync(inspection, cancellationToken);
    }

    public async Task<Inspection> LoadCompletedAsync(Guid inspectionId, CancellationToken cancellationToken)
    {
        var inspection = await QueryCompleted()
                             .FirstOrDefaultAsync(i => i.Id == inspectionId, cancellationToken)
                         ?? throw new KeyNotFoundException("Inspection was not found.");

        _currentUser.EnsureTenant(inspection);

        if (inspection.Status != InspectionStatus.Completed)
        {
            throw new InvalidOperationException("A working record is only issued after the examination is completed.");
        }

        return inspection;
    }

    public async Task<(string Html, byte[] Pdf, string FileName)> GetOrIssueDocumentAsync(
        Guid inspectionId,
        CancellationToken cancellationToken)
    {
        await _features.EnsureCertificatesAsync(cancellationToken);
        var inspection = await LoadCompletedAsync(inspectionId, cancellationToken);
        var stored = await IssueCoreAsync(inspection, cancellationToken);
        var name = $"{stored.CertificateNumber}.pdf";
        return (stored.Html, stored.Pdf, name);
    }

    public string BuildHtml(Inspection inspection) =>
        inspection.ExaminationType == ExaminationType.PuwerInspection
            ? BuildPuwerHtml(inspection)
            : BuildLolerHtml(inspection);

    public byte[] BuildPdf(Inspection inspection)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return inspection.ExaminationType == ExaminationType.PuwerInspection
            ? BuildPuwerPdf(inspection)
            : BuildLolerPdf(inspection);
    }

    private async Task<Certificate> IssueCoreAsync(Inspection inspection, CancellationToken cancellationToken)
    {
        var existing = await _db.Certificates
            .FirstOrDefaultAsync(c => c.InspectionId == inspection.Id, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        if (inspection.Status != InspectionStatus.Completed)
        {
            throw new InvalidOperationException("A working record is only issued after the examination is completed.");
        }

        var html = BuildHtml(inspection);
        var pdf = BuildPdf(inspection);
        var kind = inspection.ExaminationType == ExaminationType.PuwerInspection
            ? CertificateKind.PuwerAssessment
            : CertificateKind.LolerThoroughExamination;
        var template = kind == CertificateKind.PuwerAssessment
            ? inspection.PuwerTemplateCode ?? PuwerTemplates.ForCategory(inspection.Asset.Category).Code
            : "loler-schedule1-v1";

        var certificate = new Certificate
        {
            TenantId = inspection.TenantId,
            InspectionId = inspection.Id,
            AssetId = inspection.AssetId,
            ClientId = inspection.ClientId,
            Kind = kind,
            CertificateNumber = inspection.CertificateNumber ?? inspection.Id.ToString("N")[..8].ToUpperInvariant(),
            TemplateCode = template,
            Html = html,
            Pdf = pdf,
            IssuedAt = inspection.CompletedAt ?? DateTime.UtcNow
        };

        _db.Certificates.Add(certificate);
        await _db.SaveChangesAsync(cancellationToken);
        return certificate;
    }

    private IQueryable<Inspection> QueryCompleted() =>
        _db.Inspections
            .Include(i => i.Asset)
            .Include(i => i.Examiner)
            .Include(i => i.Defects)
            .Include(i => i.Tenant)
            .Include(i => i.Client)
            .Include(i => i.Site)
            .Include(i => i.Certificates);

    private string BuildLolerHtml(Inspection inspection)
    {
        var uk = CultureInfo.GetCultureInfo("en-GB");
        var defects = inspection.Defects.ToList();
        var defectRows = defects.Count == 0
            ? "<tr><td colspan=\"3\">None recorded.</td></tr>"
            : string.Join(string.Empty, defects.Select(d =>
                $"<tr><td>{Html(d.Severity.ToString())}</td><td>{Html(d.Category)}</td><td>{Html(d.Description)}</td></tr>"));

        return $$"""
                 <!DOCTYPE html>
                 <html lang="en-GB">
                 <head>
                   <meta charset="utf-8" />
                   <title>Record of thorough examination {{Html(inspection.CertificateNumber)}}</title>
                   <style>
                     body { font-family: "Segoe UI", Calibri, sans-serif; color: #122033; margin: 32px; }
                     h1 { font-size: 20px; margin-bottom: 4px; }
                     h2 { font-size: 14px; margin-top: 24px; border-bottom: 1px solid #c5ced8; padding-bottom: 4px; }
                     .muted { color: #5b6b7c; font-size: 12px; max-width: 720px; }
                     table { width: 100%; border-collapse: collapse; margin-top: 8px; }
                     th, td { text-align: left; padding: 6px 8px; border: 1px solid #d5dee8; font-size: 13px; vertical-align: top; }
                     th { background: #f3f6f9; width: 28%; }
                     .result { font-weight: 700; }
                   </style>
                 </head>
                 <body>
                   <h1>Record of thorough examination</h1>
                   <p class="muted">
                     This record is intended to support LOLER 1998 Schedule 1 style record-keeping for lifting equipment. It is a LiftLedger working document — not an HSE-certified form, and it does not constitute a legal determination of compliance. A competent person remains responsible under LOLER 1998 and the Health and Safety at Work etc. Act 1974.
                   </p>
                   <table>
                     <tr><th>Certificate number</th><td>{{Html(inspection.CertificateNumber)}}</td></tr>
                     <tr><th>Organisation (competent person)</th><td>{{Html(inspection.Tenant.Name)}}</td></tr>
                     <tr><th>Competent person address</th><td>{{Html(inspection.CompetentPersonAddress ?? FormatAddress(inspection.Tenant.AddressLine1, inspection.Tenant.Town, inspection.Tenant.Postcode))}}</td></tr>
                     <tr><th>Employer for whom the examination was made</th><td>{{Html(inspection.EmployerName)}} — {{Html(inspection.EmployerAddress)}}</td></tr>
                     <tr><th>Premises at which the examination was made</th><td>{{Html(inspection.PremisesAddress)}}</td></tr>
                     <tr><th>Asset</th><td>{{Html(inspection.Asset.AssetNumber)}} — {{Html(inspection.Asset.Name)}}</td></tr>
                     <tr><th>Make / model / serial</th><td>{{Html(inspection.Asset.Make)}} / {{Html(inspection.Asset.Model)}} / {{Html(inspection.Asset.SerialNumber)}}</td></tr>
                     <tr><th>Identification / QR code</th><td>{{Html(inspection.Asset.IdentificationCode)}}</td></tr>
                     <tr><th>Safe working load</th><td>{{Html(inspection.SafeWorkingLoad ?? inspection.Asset.SafeWorkingLoad)}}</td></tr>
                     <tr><th>Date of last thorough examination</th><td>{{inspection.DateOfLastExamination?.ToString("d MMMM yyyy", uk) ?? "—"}}</td></tr>
                     <tr><th>Date of this examination</th><td>{{inspection.ExaminationDate.ToString("d MMMM yyyy", uk)}}</td></tr>
                     <tr><th>Date of this report</th><td>{{(inspection.DateOfReport ?? inspection.ExaminationDate).ToString("d MMMM yyyy", uk)}}</td></tr>
                     <tr><th>Latest date of next examination</th><td>{{inspection.NextDueDate?.ToString("d MMMM yyyy", uk) ?? "—"}}</td></tr>
                     <tr><th>Type</th><td>{{Html(FormatExamType(inspection.ExaminationType))}}</td></tr>
                     <tr><th>Result</th><td class="result">{{Html(FormatResult(inspection.Result))}}</td></tr>
                     <tr><th>Examiner</th><td>{{Html(inspection.Examiner.FullName)}}{{(string.IsNullOrWhiteSpace(inspection.ExaminerQualifications) ? "" : " — " + Html(inspection.ExaminerQualifications))}}</td></tr>
                     <tr><th>Person authenticating the report</th><td>{{Html(inspection.AuthenticatingPerson)}}</td></tr>
                   </table>
                   <h2>Particulars of the examination</h2>
                   <p>{{Html(inspection.ParticularsOfExamination)}}</p>
                   <h2>Particulars of any test carried out</h2>
                   <p>{{Html(inspection.ParticularsOfTests)}}</p>
                   <h2>Particulars of any defect</h2>
                   <p>{{Html(inspection.ParticularsOfDefects)}}</p>
                   <table>
                     <tr><th>Severity</th><th>Category</th><th>Description</th></tr>
                     {{defectRows}}
                   </table>
                   <h2>Repair, renewal, alteration or further examination required</h2>
                   <p>{{Html(inspection.RepairsRequired)}}</p>
                   <h2>Declaration</h2>
                   <p>{{Html(inspection.Declaration ?? DefaultDeclaration(inspection))}}</p>
                 </body>
                 </html>
                 """;
    }

    private string BuildPuwerHtml(Inspection inspection)
    {
        var uk = CultureInfo.GetCultureInfo("en-GB");
        var template = PuwerTemplates.Find(inspection.PuwerTemplateCode)
                       ?? PuwerTemplates.ForCategory(inspection.Asset.Category);
        var answers = ParseAnswers(inspection.PuwerAnswersJson);
        var answerRows = template.Items.Select(item =>
        {
            var answer = answers.FirstOrDefault(a => a.ItemId == item.Id);
            return $"<tr><td>{Html(item.Section)}</td><td>{Html(item.Prompt)}</td><td>{Html(FormatPuwerResult(answer?.Result))}</td><td>{Html(answer?.Notes)}</td></tr>";
        });

        return $$"""
                 <!DOCTYPE html>
                 <html lang="en-GB">
                 <head>
                   <meta charset="utf-8" />
                   <title>PUWER assessment {{Html(inspection.CertificateNumber)}}</title>
                   <style>
                     body { font-family: "Segoe UI", Calibri, sans-serif; color: #122033; margin: 32px; }
                     h1 { font-size: 20px; margin-bottom: 4px; }
                     h2 { font-size: 14px; margin-top: 24px; border-bottom: 1px solid #c5ced8; padding-bottom: 4px; }
                     .muted { color: #5b6b7c; font-size: 12px; max-width: 720px; }
                     table { width: 100%; border-collapse: collapse; margin-top: 8px; }
                     th, td { text-align: left; padding: 6px 8px; border: 1px solid #d5dee8; font-size: 13px; vertical-align: top; }
                     th { background: #f3f6f9; }
                   </style>
                 </head>
                 <body>
                   <h1>{{Html(template.Title)}}</h1>
                   <p class="muted">{{Html(template.Introduction)}} A competent person remains responsible under PUWER 1998 and the Health and Safety at Work etc. Act 1974.</p>
                   <table>
                     <tr><th>Document number</th><td>{{Html(inspection.CertificateNumber)}}</td></tr>
                     <tr><th>Organisation</th><td>{{Html(inspection.Tenant.Name)}}</td></tr>
                     <tr><th>Asset</th><td>{{Html(inspection.Asset.AssetNumber)}} — {{Html(inspection.Asset.Name)}} ({{Html(inspection.Asset.Category.ToString())}})</td></tr>
                     <tr><th>Identification / QR code</th><td>{{Html(inspection.Asset.IdentificationCode)}}</td></tr>
                     <tr><th>Date of assessment</th><td>{{inspection.ExaminationDate.ToString("d MMMM yyyy", uk)}}</td></tr>
                     <tr><th>Review by</th><td>{{inspection.NextDueDate?.ToString("d MMMM yyyy", uk) ?? "—"}}</td></tr>
                     <tr><th>Result</th><td>{{Html(FormatResult(inspection.Result))}}</td></tr>
                     <tr><th>Assessor</th><td>{{Html(inspection.Examiner.FullName)}}{{(string.IsNullOrWhiteSpace(inspection.ExaminerQualifications) ? "" : " — " + Html(inspection.ExaminerQualifications))}}</td></tr>
                   </table>
                   <h2>Checklist</h2>
                   <table>
                     <tr><th>Section</th><th>Prompt</th><th>Finding</th><th>Notes</th></tr>
                     {{string.Join(string.Empty, answerRows)}}
                   </table>
                   <h2>Summary of the assessment</h2>
                   <p>{{Html(inspection.ParticularsOfExamination)}}</p>
                   <h2>Action required</h2>
                   <p>{{Html(inspection.RepairsRequired ?? inspection.ParticularsOfDefects)}}</p>
                   <h2>Declaration</h2>
                   <p>{{Html(inspection.Declaration ?? DefaultDeclaration(inspection))}}</p>
                 </body>
                 </html>
                 """;
    }

    private byte[] BuildLolerPdf(Inspection inspection)
    {
        var uk = CultureInfo.GetCultureInfo("en-GB");
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Color.FromHex("#122033")));

                page.Header().Column(col =>
                {
                    col.Item().Text("Record of thorough examination").FontSize(18).SemiBold();
                    col.Item().Text(inspection.CertificateNumber ?? "Unnumbered").FontSize(11).FontColor(Color.FromHex("#5b6b7c"));
                    col.Item().PaddingTop(8).Text(
                            "LiftLedger working record in the style of LOLER Schedule 1. Not an HSE-certified document and not a legal determination of compliance.")
                        .FontSize(8).Italic().FontColor(Color.FromHex("#5b6b7c"));
                });

                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Spacing(6);
                    Row(col, "Organisation", inspection.Tenant.Name);
                    Row(col, "Employer", $"{inspection.EmployerName} — {inspection.EmployerAddress}");
                    Row(col, "Premises", inspection.PremisesAddress ?? "—");
                    Row(col, "Asset", $"{inspection.Asset.AssetNumber} — {inspection.Asset.Name}");
                    Row(col, "Serial / ID code", $"{inspection.Asset.SerialNumber} / {inspection.Asset.IdentificationCode}");
                    Row(col, "Safe working load", inspection.SafeWorkingLoad ?? inspection.Asset.SafeWorkingLoad ?? "—");
                    Row(col, "Date of last examination", inspection.DateOfLastExamination?.ToString("d MMMM yyyy", uk) ?? "—");
                    Row(col, "Date of this examination", inspection.ExaminationDate.ToString("d MMMM yyyy", uk));
                    Row(col, "Latest date of next examination", inspection.NextDueDate?.ToString("d MMMM yyyy", uk) ?? "—");
                    Row(col, "Type", FormatExamType(inspection.ExaminationType));
                    Row(col, "Result", FormatResult(inspection.Result));
                    Row(col, "Examiner", string.IsNullOrWhiteSpace(inspection.ExaminerQualifications)
                        ? inspection.Examiner.FullName
                        : $"{inspection.Examiner.FullName} — {inspection.ExaminerQualifications}");

                    col.Item().PaddingTop(12).Text("Particulars of the examination").SemiBold();
                    col.Item().Text(inspection.ParticularsOfExamination ?? "—");
                    col.Item().PaddingTop(8).Text("Particulars of any test").SemiBold();
                    col.Item().Text(inspection.ParticularsOfTests ?? "—");
                    col.Item().PaddingTop(8).Text("Particulars of any defect").SemiBold();
                    col.Item().Text(inspection.ParticularsOfDefects ?? (inspection.Defects.Count == 0 ? "None recorded." : "See table."));

                    if (inspection.Defects.Count > 0)
                    {
                        col.Item().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(90);
                                c.ConstantColumn(90);
                                c.RelativeColumn();
                            });
                            table.Header(h =>
                            {
                                h.Cell().Background("#f3f6f9").Padding(4).Text("Severity").SemiBold();
                                h.Cell().Background("#f3f6f9").Padding(4).Text("Category").SemiBold();
                                h.Cell().Background("#f3f6f9").Padding(4).Text("Description").SemiBold();
                            });
                            foreach (var defect in inspection.Defects)
                            {
                                table.Cell().Border(0.5f).BorderColor("#d5dee8").Padding(4).Text(defect.Severity.ToString());
                                table.Cell().Border(0.5f).BorderColor("#d5dee8").Padding(4).Text(defect.Category ?? "—");
                                table.Cell().Border(0.5f).BorderColor("#d5dee8").Padding(4).Text(defect.Description);
                            }
                        });
                    }

                    col.Item().PaddingTop(8).Text("Repair, renewal, alteration or further examination required").SemiBold();
                    col.Item().Text(inspection.RepairsRequired ?? "—");
                    col.Item().PaddingTop(8).Text("Declaration").SemiBold();
                    col.Item().Text(inspection.Declaration ?? DefaultDeclaration(inspection));
                });
            });
        });

        return document.GeneratePdf();
    }

    private byte[] BuildPuwerPdf(Inspection inspection)
    {
        var uk = CultureInfo.GetCultureInfo("en-GB");
        var template = PuwerTemplates.Find(inspection.PuwerTemplateCode)
                       ?? PuwerTemplates.ForCategory(inspection.Asset.Category);
        var answers = ParseAnswers(inspection.PuwerAnswersJson);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Color.FromHex("#122033")));
                page.Header().Column(col =>
                {
                    col.Item().Text(template.Title).FontSize(16).SemiBold();
                    col.Item().Text(inspection.CertificateNumber ?? "Unnumbered").FontSize(11).FontColor(Color.FromHex("#5b6b7c"));
                    col.Item().PaddingTop(8).Text(template.Introduction).FontSize(8).Italic().FontColor(Color.FromHex("#5b6b7c"));
                });
                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Spacing(6);
                    Row(col, "Organisation", inspection.Tenant.Name);
                    Row(col, "Asset", $"{inspection.Asset.AssetNumber} — {inspection.Asset.Name}");
                    Row(col, "Date", inspection.ExaminationDate.ToString("d MMMM yyyy", uk));
                    Row(col, "Result", FormatResult(inspection.Result));
                    Row(col, "Assessor", inspection.Examiner.FullName);
                    col.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(80);
                            c.RelativeColumn();
                            c.ConstantColumn(90);
                            c.RelativeColumn();
                        });
                        table.Header(h =>
                        {
                            h.Cell().Background("#f3f6f9").Padding(4).Text("Section").SemiBold();
                            h.Cell().Background("#f3f6f9").Padding(4).Text("Prompt").SemiBold();
                            h.Cell().Background("#f3f6f9").Padding(4).Text("Finding").SemiBold();
                            h.Cell().Background("#f3f6f9").Padding(4).Text("Notes").SemiBold();
                        });
                        foreach (var item in template.Items)
                        {
                            var answer = answers.FirstOrDefault(a => a.ItemId == item.Id);
                            table.Cell().Border(0.5f).BorderColor("#d5dee8").Padding(4).Text(item.Section);
                            table.Cell().Border(0.5f).BorderColor("#d5dee8").Padding(4).Text(item.Prompt);
                            table.Cell().Border(0.5f).BorderColor("#d5dee8").Padding(4).Text(FormatPuwerResult(answer?.Result));
                            table.Cell().Border(0.5f).BorderColor("#d5dee8").Padding(4).Text(answer?.Notes ?? "—");
                        }
                    });
                    col.Item().PaddingTop(8).Text("Summary").SemiBold();
                    col.Item().Text(inspection.ParticularsOfExamination ?? "—");
                    col.Item().PaddingTop(8).Text("Action required").SemiBold();
                    col.Item().Text(inspection.RepairsRequired ?? inspection.ParticularsOfDefects ?? "—");
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void Row(ColumnDescriptor col, string label, string? value)
    {
        col.Item().Row(row =>
        {
            row.ConstantItem(170).Text(label).SemiBold();
            row.RelativeItem().Text(string.IsNullOrWhiteSpace(value) ? "—" : value);
        });
    }

    private static IReadOnlyList<PuwerAnswerDto> ParseAnswers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<PuwerAnswerDto>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string DefaultDeclaration(Inspection inspection) =>
        inspection.Result == InspectionResult.Fail
        || inspection.Defects.Any(d => d.RequiresImmediateWithdrawal)
            ? "This equipment must not be used until the recorded defects have been remedied and, where required, a further examination has been completed."
            : "In the opinion of the examiner this equipment may remain in service until the latest date of the next examination, subject to any recorded defects being monitored or remedied.";

    private static string FormatExamType(ExaminationType type) => type switch
    {
        ExaminationType.ThoroughExamination => "Thorough examination (LOLER)",
        ExaminationType.InterimInspection => "Interim inspection",
        ExaminationType.PreUseCheck => "Pre-use check",
        ExaminationType.PuwerInspection => "PUWER assessment",
        _ => type.ToString()
    };

    private static string FormatResult(InspectionResult? result) => result switch
    {
        InspectionResult.Pass => "Pass",
        InspectionResult.PassWithDefects => "Pass with defects",
        InspectionResult.Fail => "Fail — withdraw from service",
        _ => "—"
    };

    private static string FormatPuwerResult(PuwerItemResult? result) => result switch
    {
        PuwerItemResult.Suitable => "Suitable",
        PuwerItemResult.ActionRequired => "Action required",
        PuwerItemResult.NotApplicable => "Not applicable",
        _ => "—"
    };

    private static string? FormatAddress(string? line, string? town, string? postcode) =>
        string.Join(", ", new[] { line, town, postcode }.Where(v => !string.IsNullOrWhiteSpace(v)));

    private static string Html(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : WebUtility.HtmlEncode(value);

    internal static CertificateSummary MapSummary(Certificate certificate) =>
        new(
            certificate.Id,
            certificate.InspectionId,
            certificate.AssetId,
            certificate.ClientId,
            certificate.Kind,
            certificate.CertificateNumber,
            certificate.TemplateCode,
            certificate.Asset?.AssetNumber ?? string.Empty,
            certificate.Asset?.Name ?? string.Empty,
            certificate.IssuedAt);
}
