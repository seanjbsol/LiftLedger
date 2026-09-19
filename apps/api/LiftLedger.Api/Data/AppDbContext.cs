using System.Linq.Expressions;
using System.Reflection;
using LiftLedger.Api.Auth;
using LiftLedger.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LiftLedger.Api.Data;

public class AppDbContext : DbContext
{
    private readonly ICurrentUser _currentUser;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<Defect> Defects => Set<Defect>();
    public DbSet<DefectPhoto> DefectPhotos => Set<DefectPhoto>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<ClientPortalToken> ClientPortalTokens => Set<ClientPortalToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.TradingName).HasMaxLength(200);
            entity.Property(e => e.Postcode).HasMaxLength(16);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(512).IsRequired();
        });

        modelBuilder.Entity<Membership>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.UserId }).IsUnique();
            entity.HasOne(e => e.Tenant).WithMany(t => t.Memberships).HasForeignKey(e => e.TenantId);
            entity.HasOne(e => e.User).WithMany(u => u.Memberships).HasForeignKey(e => e.UserId);
            entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<Client>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.Name });
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Site>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Client).WithMany(c => c.Sites).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AssetNumber).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.IdentificationCode).HasMaxLength(128);
            entity.Property(e => e.SafeWorkingLoad).HasMaxLength(64);
            entity.Property(e => e.Category).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(e => new { e.TenantId, e.AssetNumber }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.IdentificationCode });
            entity.HasOne(e => e.Tenant).WithMany(t => t.Assets).HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Client).WithMany(c => c.Assets).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Site).WithMany(s => s.Assets).HasForeignKey(e => e.SiteId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Inspection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CertificateNumber).HasMaxLength(64);
            entity.Property(e => e.SafeWorkingLoad).HasMaxLength(64);
            entity.Property(e => e.ExaminationType).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.Result).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(e => new { e.TenantId, e.CertificateNumber })
                .IsUnique()
                .HasFilter("CertificateNumber IS NOT NULL");
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Asset).WithMany(a => a.Inspections).HasForeignKey(e => e.AssetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Examiner).WithMany().HasForeignKey(e => e.ExaminerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Client).WithMany().HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Site).WithMany().HasForeignKey(e => e.SiteId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Defect>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.Severity).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasOne(e => e.Inspection).WithMany(i => i.Defects).HasForeignKey(e => e.InspectionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Asset).WithMany().HasForeignKey(e => e.AssetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AssignedTo).WithMany().HasForeignKey(e => e.AssignedToUserId).OnDelete(DeleteBehavior.NoAction).IsRequired(false);
            entity.HasOne(e => e.ClosedBy).WithMany().HasForeignKey(e => e.ClosedByUserId).OnDelete(DeleteBehavior.NoAction).IsRequired(false);
            entity.HasOne(e => e.RetestInspection).WithMany().HasForeignKey(e => e.RetestInspectionId).OnDelete(DeleteBehavior.NoAction).IsRequired(false);
        });

        modelBuilder.Entity<DefectPhoto>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Kind).HasConversion<string>().HasMaxLength(16);
            entity.HasOne(e => e.Defect).WithMany(d => d.Photos).HasForeignKey(e => e.DefectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.UploadedBy).WithMany().HasForeignKey(e => e.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Certificate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CertificateNumber).HasMaxLength(64).IsRequired();
            entity.Property(e => e.TemplateCode).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Kind).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(e => new { e.TenantId, e.InspectionId }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.CertificateNumber });
            entity.HasOne(e => e.Inspection).WithMany(i => i.Certificates).HasForeignKey(e => e.InspectionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Asset).WithMany().HasForeignKey(e => e.AssetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Client).WithMany().HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ClientPortalToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Label).HasMaxLength(200);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Client).WithMany().HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Cascade);
        });

        ApplyTenantQueryFilters(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTenantGuards();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTenantGuards();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyTenantGuards()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State is EntityState.Added)
            {
                if (entry.Entity.TenantId == Guid.Empty && _currentUser.IsAuthenticated)
                {
                    entry.Entity.TenantId = _currentUser.TenantId;
                }

                if (_currentUser.IsAuthenticated && entry.Entity.TenantId != _currentUser.TenantId)
                {
                    throw new InvalidOperationException("Cross-tenant write blocked.");
                }
            }

            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                GuardExistingTenant(entry);
            }
        }
    }

    private void GuardExistingTenant(EntityEntry<ITenantEntity> entry)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return;
        }

        var tenantId = entry.Entity.TenantId;
        if (entry.OriginalValues.Properties.Any(p => p.Name == nameof(ITenantEntity.TenantId)))
        {
            var original = entry.OriginalValues[nameof(ITenantEntity.TenantId)];
            if (original is Guid originalId)
            {
                tenantId = originalId;
            }
        }

        if (tenantId != _currentUser.TenantId || entry.Entity.TenantId != _currentUser.TenantId)
        {
            throw new InvalidOperationException("Cross-tenant write blocked.");
        }
    }

    /// <summary>
    /// Tenant isolation: authenticated requests only see their own TenantId.
    /// Unauthenticated scopes (register, seed) see nothing via these filters —
    /// login uses IgnoreQueryFilters for membership lookup.
    /// </summary>
    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var method = typeof(AppDbContext)
                .GetMethod(nameof(SetTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);
            method.Invoke(this, new object[] { modelBuilder });
        }
    }

    private void SetTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity
    {
        Expression<Func<TEntity, bool>> filter = entity =>
            _currentUser.IsAuthenticated && entity.TenantId == _currentUser.TenantId;

        modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
    }
}
