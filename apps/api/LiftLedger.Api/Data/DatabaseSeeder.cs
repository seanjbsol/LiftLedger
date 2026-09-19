using LiftLedger.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LiftLedger.Api.Data;

public static class DatabaseSeeder
{
    public static readonly Guid HumberTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid NorthernTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid HumberOwnerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    public static readonly Guid NorthernOwnerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1");

    public const string DemoPassword = "DemoPass123!";

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Tenants.AnyAsync(cancellationToken))
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword);

        var humber = new Tenant
        {
            Id = HumberTenantId,
            Name = "Humber Plant & Trailer Hire Ltd",
            TradingName = "Humber Hire",
            AddressLine1 = "Westgate Works",
            Town = "Grimsby",
            Postcode = "DN31 2TG",
            Phone = "01472 000000"
        };

        var northern = new Tenant
        {
            Id = NorthernTenantId,
            Name = "Northern Plant Examiners Ltd",
            TradingName = "Northern Examiners",
            AddressLine1 = "Riverside Industrial Estate",
            Town = "Scunthorpe",
            Postcode = "DN15 8QW",
            Phone = "01724 000000"
        };

        var humberOwner = new User
        {
            Id = HumberOwnerId,
            Email = "owner@humberhire.demo",
            FullName = "Alex Houghton",
            PasswordHash = passwordHash
        };

        var humberExaminer = new User
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"),
            Email = "examiner@humberhire.demo",
            FullName = "Jordan Blake",
            PasswordHash = passwordHash
        };

        var northernOwner = new User
        {
            Id = NorthernOwnerId,
            Email = "owner@northernpplant.demo",
            FullName = "Sam Reed",
            PasswordHash = passwordHash
        };

        var client = new Client
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333331"),
            TenantId = HumberTenantId,
            Name = "North Lincs Civils Ltd",
            ContactName = "Pat Singh",
            Email = "plant@nlcivils.demo",
            Phone = "01469 000000",
            Town = "Immingham",
            Postcode = "DN40 2LZ"
        };

        var site = new Site
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444441"),
            TenantId = HumberTenantId,
            ClientId = client.Id,
            Name = "Immingham yard",
            AddressLine1 = "Kings Road",
            Town = "Immingham",
            Postcode = "DN40 1QR"
        };

        var trailer = Asset(
            "55555555-5555-5555-5555-555555555551",
            "TRI-1042",
            "Curtainsider trailer",
            AssetCategory.Trailer,
            "SDC",
            "C-D 13.6m",
            "SDC1042HUM",
            "QR-TRI-1042",
            null,
            today.AddDays(10),
            client.Id,
            site.Id);

        var forklift = Asset(
            "55555555-5555-5555-5555-555555555552",
            "FLT-07",
            "Counterbalance forklift",
            AssetCategory.Plant,
            "Toyota",
            "8FDF25",
            "TY-FLT-07",
            "QR-FLT-07",
            "2,500 kg",
            today.AddDays(-14));

        var sling = Asset(
            "55555555-5555-5555-5555-555555555553",
            "CS-16",
            "Grade 8 chain sling",
            AssetCategory.LiftingAccessory,
            "William Hackett",
            "4-leg 2t",
            "WH-CS-16",
            "QR-CS-16",
            "2 t",
            today.AddMonths(5));

        var mewp = Asset(
            "55555555-5555-5555-5555-555555555554",
            "MEWP-03",
            "Articulated boom",
            AssetCategory.LiftingEquipment,
            "Genie",
            "Z-45/25",
            "GN-MEWP-03",
            "QR-MEWP-03",
            "227 kg platform",
            today.AddDays(60));

        var plantTrailer = Asset(
            "55555555-5555-5555-5555-555555555555",
            "TRAIL-22",
            "Plant trailer",
            AssetCategory.Trailer,
            "Ifor Williams",
            "GX126",
            "IW-TRAIL-22",
            "QR-TRAIL-22",
            null,
            today.AddDays(5));

        var northernHoist = Asset(
            "55555555-5555-5555-5555-555555555561",
            "HOIST-01",
            "Electric chain hoist",
            AssetCategory.LiftingEquipment,
            "Yale",
            "CPE 2-1",
            "YALE-NTH-01",
            "QR-HOIST-01",
            "2,000 kg",
            today.AddDays(21),
            tenantId: NorthernTenantId);

        var recentSlingExam = new Inspection
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666661"),
            TenantId = HumberTenantId,
            AssetId = sling.Id,
            ExaminerUserId = humberExaminer.Id,
            ExaminationType = ExaminationType.ThoroughExamination,
            Status = InspectionStatus.Completed,
            Result = InspectionResult.Pass,
            ExaminationDate = today.AddDays(-12),
            NextDueDate = sling.NextExaminationDue,
            SafeWorkingLoad = sling.SafeWorkingLoad,
            ParticularsOfExamination = "Visual and functional examination of chain, master link, hooks and safety latches. Identification and SWL markings legible.",
            ExaminerQualifications = "LEEAA affiliated examiner (demo record)",
            CertificateNumber = "LL-HUM-2026-0001",
            CompletedAt = DateTime.UtcNow.AddDays(-12)
        };

        var overdueForkliftExam = new Inspection
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666662"),
            TenantId = HumberTenantId,
            AssetId = forklift.Id,
            ExaminerUserId = humberExaminer.Id,
            ExaminationType = ExaminationType.ThoroughExamination,
            Status = InspectionStatus.Completed,
            Result = InspectionResult.PassWithDefects,
            ExaminationDate = today.AddMonths(-6).AddDays(-14),
            NextDueDate = forklift.NextExaminationDue,
            SafeWorkingLoad = forklift.SafeWorkingLoad,
            ParticularsOfExamination = "Thorough examination of lifting chains, carriage, forks and hydraulics.",
            ParticularsOfDefects = "Fork heel wear approaching rejection limit; mast chain stretch to be monitored.",
            RepairsRequired = "Replace forks at next service. Re-examine chains at next thorough examination.",
            ExaminerQualifications = "LEEAA affiliated examiner (demo record)",
            CertificateNumber = "LL-HUM-2026-0002",
            CompletedAt = DateTime.UtcNow.AddMonths(-6).AddDays(-14)
        };

        var forkliftDefect = new Defect
        {
            Id = Guid.Parse("77777777-7777-7777-7777-777777777771"),
            TenantId = HumberTenantId,
            InspectionId = overdueForkliftExam.Id,
            AssetId = forklift.Id,
            Description = "Fork heel wear approaching rejection limit on both forks.",
            Severity = DefectSeverity.Defect,
            Category = "Forks",
            RequiresImmediateWithdrawal = false,
            Notes = "Still within serviceable limits at time of examination. Plan replacement."
        };

        var northernExam = new Inspection
        {
            Id = Guid.Parse("66666666-6666-6666-6666-666666666671"),
            TenantId = NorthernTenantId,
            AssetId = northernHoist.Id,
            ExaminerUserId = northernOwner.Id,
            ExaminationType = ExaminationType.ThoroughExamination,
            Status = InspectionStatus.Completed,
            Result = InspectionResult.Pass,
            ExaminationDate = today.AddDays(-5),
            NextDueDate = northernHoist.NextExaminationDue,
            SafeWorkingLoad = northernHoist.SafeWorkingLoad,
            ParticularsOfExamination = "Load chain, hook, brake and limit switches examined. Functional hoist and lower test completed.",
            ExaminerQualifications = "Competent person (demo record)",
            CertificateNumber = "LL-NTH-2026-0001",
            CompletedAt = DateTime.UtcNow.AddDays(-5)
        };

        db.Tenants.AddRange(humber, northern);
        db.Users.AddRange(humberOwner, humberExaminer, northernOwner);
        db.Memberships.AddRange(
            new Membership { TenantId = HumberTenantId, UserId = humberOwner.Id, Role = MembershipRole.Owner },
            new Membership { TenantId = HumberTenantId, UserId = humberExaminer.Id, Role = MembershipRole.Examiner },
            new Membership { TenantId = NorthernTenantId, UserId = northernOwner.Id, Role = MembershipRole.Owner });
        db.Clients.Add(client);
        db.Sites.Add(site);
        db.Assets.AddRange(trailer, forklift, sling, mewp, plantTrailer, northernHoist);
        db.Inspections.AddRange(recentSlingExam, overdueForkliftExam, northernExam);
        db.Defects.Add(forkliftDefect);

        await db.SaveChangesAsync(cancellationToken);
        return;

        Asset Asset(
            string id,
            string number,
            string name,
            AssetCategory category,
            string make,
            string model,
            string serial,
            string code,
            string? swl,
            DateOnly? nextDue,
            Guid? clientId = null,
            Guid? siteId = null,
            Guid? tenantId = null) =>
            new()
            {
                Id = Guid.Parse(id),
                TenantId = tenantId ?? HumberTenantId,
                ClientId = clientId,
                SiteId = siteId,
                AssetNumber = number,
                Name = name,
                Category = category,
                Make = make,
                Model = model,
                SerialNumber = serial,
                IdentificationCode = code,
                SafeWorkingLoad = swl,
                FirstUseDate = today.AddYears(-3),
                NextExaminationDue = nextDue,
                Notes = "Demo seed asset — not live plant."
            };
    }
}
