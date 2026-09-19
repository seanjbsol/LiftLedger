using System.Globalization;
using System.Net;
using LiftLedger.Api.Auth;
using LiftLedger.Api.Data;
using LiftLedger.Api.Domain;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LiftLedger.Api.Services;

public class CertificateService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CertificateService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Inspection> LoadCompletedAsync(Guid inspectionId, CancellationToken cancellationToken)
    {
        var inspection = await _db.Inspections
                             .Include(i => i.Asset)
                             .Include(i => i.Examiner)
                             .Include(i => i.Defects)
                             .Include(i => i.Tenant)
                             .FirstOrDefaultAsync(i => i.Id == inspectionId, cancellationToken)
                         ?? throw new KeyNotFoundException("Inspection was not found.");

        _currentUser.EnsureTenant(inspection);

        if (inspection.Status != InspectionStatus.Completed)
        {
            throw new InvalidOperationException("A record of thorough examination is only issued after the examination is completed.");
        }

        return inspection;
    }

    public string BuildHtml(Inspection inspection)
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
                     This record is intended to support LOLER 1998 Schedule 1 style record-keeping for lifting equipment and, where used, PUWER inspection notes for plant. It is a LiftLedger working document — not an HSE-certified form, and it does not constitute a legal determination of compliance.
                   </p>
                   <table>
                     <tr><th>Certificate number</th><td>{{Html(inspection.CertificateNumber)}}</td></tr>
                     <tr><th>Organisation</th><td>{{Html(inspection.Tenant.Name)}}</td></tr>
                     <tr><th>Asset</th><td>{{Html(inspection.Asset.AssetNumber)}} — {{Html(inspection.Asset.Name)}}</td></tr>
                     <tr><th>Serial / ID code</th><td>{{Html(inspection.Asset.SerialNumber)}} / {{Html(inspection.Asset.IdentificationCode)}}</td></tr>
                     <tr><th>Safe working load</th><td>{{Html(inspection.SafeWorkingLoad ?? inspection.Asset.SafeWorkingLoad)}}</td></tr>
                     <tr><th>Date of this examination</th><td>{{inspection.ExaminationDate.ToString("d MMMM yyyy", uk)}}</td></tr>
                     <tr><th>Latest date of next examination</th><td>{{inspection.NextDueDate?.ToString("d MMMM yyyy", uk) ?? "—"}}</td></tr>
                     <tr><th>Type</th><td>{{Html(FormatExamType(inspection.ExaminationType))}}</td></tr>
                     <tr><th>Result</th><td class="result">{{Html(FormatResult(inspection.Result))}}</td></tr>
                     <tr><th>Examiner</th><td>{{Html(inspection.Examiner.FullName)}}{{(string.IsNullOrWhiteSpace(inspection.ExaminerQualifications) ? "" : " — " + Html(inspection.ExaminerQualifications))}}</td></tr>
                   </table>
                   <h2>Particulars of the examination</h2>
                   <p>{{Html(inspection.ParticularsOfExamination) }}</p>
                   <h2>Particulars of any defect</h2>
                   <p>{{Html(inspection.ParticularsOfDefects)}}</p>
                   <table>
                     <tr><th>Severity</th><th>Category</th><th>Description</th></tr>
                     {{defectRows}}
                   </table>
                   <h2>Repair, alteration or further examination required</h2>
                   <p>{{Html(inspection.RepairsRequired)}}</p>
                 </body>
                 </html>
                 """;
    }

    public byte[] BuildPdf(Inspection inspection)
    {
        QuestPDF.Settings.License = LicenseType.Community;
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
                    Row(col, "Asset", $"{inspection.Asset.AssetNumber} — {inspection.Asset.Name}");
                    Row(col, "Serial / ID code", $"{inspection.Asset.SerialNumber} / {inspection.Asset.IdentificationCode}");
                    Row(col, "Safe working load", inspection.SafeWorkingLoad ?? inspection.Asset.SafeWorkingLoad ?? "—");
                    Row(col, "Date of this examination", inspection.ExaminationDate.ToString("d MMMM yyyy", uk));
                    Row(col, "Latest date of next examination", inspection.NextDueDate?.ToString("d MMMM yyyy", uk) ?? "—");
                    Row(col, "Type", FormatExamType(inspection.ExaminationType));
                    Row(col, "Result", FormatResult(inspection.Result));
                    Row(col, "Examiner", string.IsNullOrWhiteSpace(inspection.ExaminerQualifications)
                        ? inspection.Examiner.FullName
                        : $"{inspection.Examiner.FullName} — {inspection.ExaminerQualifications}");

                    col.Item().PaddingTop(12).Text("Particulars of the examination").SemiBold();
                    col.Item().Text(inspection.ParticularsOfExamination ?? "—");
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

                    col.Item().PaddingTop(8).Text("Repair, alteration or further examination required").SemiBold();
                    col.Item().Text(inspection.RepairsRequired ?? "—");
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void Row(ColumnDescriptor col, string label, string value)
    {
        col.Item().Row(row =>
        {
            row.ConstantItem(170).Text(label).SemiBold();
            row.RelativeItem().Text(value);
        });
    }

    private static string FormatExamType(ExaminationType type) => type switch
    {
        ExaminationType.ThoroughExamination => "Thorough examination (LOLER)",
        ExaminationType.InterimInspection => "Interim inspection",
        ExaminationType.PreUseCheck => "Pre-use check",
        ExaminationType.PuwerInspection => "PUWER inspection",
        _ => type.ToString()
    };

    private static string FormatResult(InspectionResult? result) => result switch
    {
        InspectionResult.Pass => "Pass",
        InspectionResult.PassWithDefects => "Pass with defects",
        InspectionResult.Fail => "Fail — withdraw from service",
        _ => "—"
    };

    private static string Html(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : WebUtility.HtmlEncode(value);
}
