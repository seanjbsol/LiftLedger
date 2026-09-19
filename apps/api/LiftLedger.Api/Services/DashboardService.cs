using LiftLedger.Api.Auth;
using LiftLedger.Api.Contracts;
using LiftLedger.Api.Data;
using LiftLedger.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LiftLedger.Api.Services;

public class DashboardService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public DashboardService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<DashboardResponse> GetAsync(CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.AsNoTracking()
                         .FirstOrDefaultAsync(t => t.Id == _currentUser.TenantId, cancellationToken)
                     ?? throw new KeyNotFoundException("Organisation was not found.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dueSoonUntil = today.AddDays(DueStatus.DueSoonDays);

        var assets = await _db.Assets.AsNoTracking()
            .Include(a => a.Client)
            .Include(a => a.Site)
            .Where(a => !a.IsArchived)
            .ToListAsync(cancellationToken);

        var overdue = assets
            .Where(a => a.NextExaminationDue is not null && a.NextExaminationDue < today)
            .OrderBy(a => a.NextExaminationDue)
            .Select(AssetService.MapSummary)
            .ToList();

        var dueSoon = assets
            .Where(a => a.NextExaminationDue is not null
                        && a.NextExaminationDue >= today
                        && a.NextExaminationDue <= dueSoonUntil)
            .OrderBy(a => a.NextExaminationDue)
            .Select(AssetService.MapSummary)
            .ToList();

        var recent = await _db.Inspections.AsNoTracking()
            .Include(i => i.Asset)
            .Include(i => i.Examiner)
            .Include(i => i.Certificates)
            .OrderByDescending(i => i.CompletedAt ?? i.CreatedAt)
            .Take(8)
            .ToListAsync(cancellationToken);

        var openDefects = await _db.Defects.AsNoTracking()
            .CountAsync(d => d.Status != DefectStatus.Closed && d.Severity != DefectSeverity.Observation, cancellationToken);

        return new DashboardResponse(
            AuthService.MapTenant(tenant),
            assets.Count,
            overdue.Count,
            dueSoon.Count,
            openDefects,
            overdue,
            dueSoon,
            recent.Select(i => Mapping.ToSummary(i)).ToList());
    }
}
