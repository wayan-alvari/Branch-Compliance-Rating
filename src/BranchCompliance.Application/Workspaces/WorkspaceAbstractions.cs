namespace BranchCompliance.Application.Workspaces;

public interface IClock { DateTime UtcNow { get; } }

public interface IWorkspaceContext { Guid WorkspaceId { get; } }

public sealed class WorkspaceContext : IWorkspaceContext
{
    public Guid WorkspaceId { get; private set; }
    public void Activate(Guid workspaceId)
    {
        if (workspaceId == Guid.Empty || (WorkspaceId != Guid.Empty && WorkspaceId != workspaceId))
            throw new InvalidOperationException("A request may access only one workspace.");
        WorkspaceId = workspaceId;
    }
}

public interface IWorkspaceLifecycle
{
    Task<Guid> ResolveAsync(Guid? cookieWorkspaceId, bool authenticatedActivity, CancellationToken cancellationToken);
    Task CleanupAsync(CancellationToken cancellationToken);
}

public interface IWorkspaceSeeder
{
    Task SeedAsync(Guid workspaceId, CancellationToken cancellationToken);
}
