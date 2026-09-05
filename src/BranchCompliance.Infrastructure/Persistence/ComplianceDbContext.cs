using System.Text.RegularExpressions;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Workspaces;
using BranchCompliance.Infrastructure.Identity;
using BranchCompliance.Infrastructure.Workspaces;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BranchCompliance.Infrastructure.Persistence;

public partial class ComplianceDbContext(DbContextOptions<ComplianceDbContext> options, IWorkspaceContext workspace)
    : IdentityDbContext<DemoUser>(options)
{
    public Guid CurrentWorkspaceId => workspace.WorkspaceId;
    public DbSet<DemoWorkspace> Workspaces => Set<DemoWorkspace>();
    public DbSet<WorkspaceRedirect> WorkspaceRedirects => Set<WorkspaceRedirect>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        if (Database.IsMySql())
        {
            builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        }

        builder.Entity<DemoWorkspace>().HasKey(row => row.WorkspaceId);
        builder.Entity<DemoWorkspace>().HasIndex(row => row.ExpiresAtUtc);
        builder.Entity<WorkspaceRedirect>().HasKey(row => row.WorkspaceId);
        builder.Entity<WorkspaceRedirect>().HasIndex(row => row.RetiredAtUtc);
        ConfigureWorkspaceEntity<AuditEvent>(builder);
        builder.Entity<AuditEvent>().Property(row => row.ActorId).HasMaxLength(128);
        builder.Entity<AuditEvent>().Property(row => row.Action).HasMaxLength(100);
        builder.Entity<AuditEvent>().Property(row => row.Details).HasMaxLength(500);
        builder.Entity<AuditEvent>().HasIndex(row => new { row.WorkspaceId, row.AtUtc });

        foreach (var entity in builder.Model.GetEntityTypes())
        {
            entity.SetTableName(SnakeCase(entity.GetTableName()!));
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(SnakeCase(property.Name));
            }
        }
    }

    private void ConfigureWorkspaceEntity<T>(ModelBuilder builder) where T : WorkspaceEntity
    {
        var entity = builder.Entity<T>();
        entity.Ignore(row => row.PendingAudit);
        entity.HasBaseType((Type?)null);
        entity.HasKey(row => row.Id);
        entity.HasIndex(row => new { row.WorkspaceId, row.Id }).IsUnique();
        entity.HasQueryFilter(row => row.WorkspaceId == CurrentWorkspaceId);
        entity.HasOne<DemoWorkspace>().WithMany().HasForeignKey(row => row.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateWorkspaceWrites();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateWorkspaceWrites();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidateWorkspaceWrites()
    {
        foreach (var entry in ChangeTracker.Entries<WorkspaceEntity>().Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (CurrentWorkspaceId == Guid.Empty || entry.Entity.WorkspaceId != CurrentWorkspaceId ||
                entry.State != EntityState.Added && entry.OriginalValues.GetValue<Guid>(nameof(WorkspaceEntity.WorkspaceId)) != CurrentWorkspaceId)
                throw new UnauthorizedAccessException("The record does not belong to this workspace.");
            if (entry.Entity is AuditEvent && entry.State != EntityState.Added)
                throw new InvalidOperationException("Audit history is immutable.");
        }
    }

    private static string SnakeCase(string value) => WordBoundary().Replace(value, "$1_$2").ToLowerInvariant();

    [GeneratedRegex("([a-z0-9])([A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex WordBoundary();
}
