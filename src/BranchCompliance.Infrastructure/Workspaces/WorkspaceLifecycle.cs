using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Workspaces;
using BranchCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BranchCompliance.Infrastructure.Workspaces;

// Called while holding the process coordinator lease (middleware or cleanup).
public sealed class WorkspaceLifecycle(ComplianceDbContext db, WorkspaceContext context, IClock clock,
    WorkspaceCoordinator coordinator, IWorkspaceSeeder seeder, WorkspaceFileStore files, IConfiguration configuration) : IWorkspaceLifecycle
{
    private static readonly TimeSpan ActivityWriteThrottle = TimeSpan.FromMinutes(1);

    public async Task<Guid> ResolveAsync(Guid? cookieWorkspaceId, bool authenticatedActivity, CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("DemoMode:Enabled")) throw new InvalidOperationException("Demo workspace initialization is disabled.");
        var now = clock.UtcNow;
        var requested = cookieWorkspaceId;
        for (var hops = 0; requested is not null && hops < 64; hops++)
        {
            var replacement = await db.WorkspaceRedirects.AsNoTracking()
                .Where(row => row.WorkspaceId == requested).Select(row => (Guid?)row.ReplacementWorkspaceId)
                .SingleOrDefaultAsync(cancellationToken);
            if (replacement is null) break;
            requested = replacement;
        }

        var workspace = requested is null ? null : await db.Workspaces.SingleOrDefaultAsync(row => row.WorkspaceId == requested, cancellationToken);
        var lastObserved = workspace is null ? null : await LastObservedAsync(workspace, cancellationToken);
        if (workspace is not null && !workspace.IsExpired(now, lastObserved))
        {
            context.Activate(workspace.WorkspaceId);
            if (authenticatedActivity)
            {
                // The small atomic marker retains exact activity across restart,
                // while database timestamp writes remain throttled.
                await files.RecordActivityAsync(workspace.WorkspaceId, now, cancellationToken);
                coordinator.Observe(workspace.WorkspaceId, now);
                if (now - workspace.LastActivityAtUtc >= ActivityWriteThrottle)
                {
                    workspace.Touch(now);
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
            return workspace.WorkspaceId;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var newId = Guid.NewGuid();
        if (workspace is not null)
        {
            files.RemoveWorkspace(workspace.WorkspaceId);
            await db.DeleteExpiredWorkspaceRowsAsync(workspace.WorkspaceId, cancellationToken);
            db.Entry(workspace).State = EntityState.Detached;
            coordinator.Forget(workspace.WorkspaceId);
        }
        if (requested is not null)
        {
            db.WorkspaceRedirects.Add(new WorkspaceRedirect { WorkspaceId = requested.Value, ReplacementWorkspaceId = newId, RetiredAtUtc = now });
        }
        context.Activate(newId);
        db.Workspaces.Add(new DemoWorkspace(newId, now, 1));
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            await seeder.SeedAsync(newId, cancellationToken);
        }
        catch
        {
            files.RemoveWorkspace(newId);
            throw;
        }
        await transaction.CommitAsync(cancellationToken);
        await files.RecordActivityAsync(newId, now, cancellationToken);
        coordinator.Observe(newId, now);
        return newId;
    }

    public async Task CleanupAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("DemoMode:Enabled")) return;
        var now = clock.UtcNow;
        var workspaces = await db.Workspaces.ToListAsync(cancellationToken);
        foreach (var workspace in workspaces)
        {
            var observed = await LastObservedAsync(workspace, cancellationToken);
            if (workspace.IsExpired(now, observed))
            {
                files.RemoveWorkspace(workspace.WorkspaceId);
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                await db.DeleteExpiredWorkspaceRowsAsync(workspace.WorkspaceId, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                db.Entry(workspace).State = EntityState.Detached;
                coordinator.Forget(workspace.WorkspaceId);
            }
            else if (observed > workspace.LastActivityAtUtc)
            {
                workspace.Touch(observed.Value);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        var cutoff = now.AddDays(-30);
        await db.WorkspaceRedirects.Where(row => row.RetiredAtUtc < cutoff).ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<DateTime?> LastObservedAsync(DemoWorkspace workspace, CancellationToken cancellationToken)
    {
        var observed = coordinator.LastObserved(workspace.WorkspaceId)
            ?? await files.ReadActivityAsync(workspace.WorkspaceId, cancellationToken);
        if (observed is null || observed < workspace.LastActivityAtUtc) observed = workspace.LastActivityAtUtc;
        coordinator.Observe(workspace.WorkspaceId, observed.Value);
        return observed;
    }
}
