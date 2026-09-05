using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Workspaces;
using BranchCompliance.Infrastructure.Persistence;

namespace BranchCompliance.Infrastructure.Workspaces;

public sealed class WorkspaceSeeder(ComplianceDbContext db, IClock clock) : IWorkspaceSeeder
{
    public async Task SeedAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        db.AuditEvents.Add(new AuditEvent(workspaceId, "system", "Workspace created", workspaceId,
            clock.UtcNow, "A fresh fictional demo workspace was initialized."));
        await db.SaveChangesAsync(cancellationToken);
    }
}
