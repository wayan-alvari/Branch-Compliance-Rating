using BranchCompliance.Application.Security;
using BranchCompliance.Application.Workspaces;

namespace BranchCompliance.Application.Dashboard;

public interface IDashboardStore
{
    Task<DashboardModel> ReadAsync(Guid workspaceId, Actor actor, CancellationToken cancellationToken);
}

public sealed class DashboardService(IDashboardStore store, IWorkspaceContext workspace, ICurrentActor currentActor)
{
    public Task<DashboardModel> GetAsync(CancellationToken cancellationToken)
    {
        var actor = currentActor.Get();
        AccessRules.RequireRole(actor, DemoRoles.All);
        if (workspace.WorkspaceId == Guid.Empty) throw new AccessDeniedException();
        return store.ReadAsync(workspace.WorkspaceId, actor, cancellationToken);
    }
}
