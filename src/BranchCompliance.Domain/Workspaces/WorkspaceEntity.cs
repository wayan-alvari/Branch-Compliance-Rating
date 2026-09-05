namespace BranchCompliance.Domain.Workspaces;

public abstract class WorkspaceEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; protected set; }
    protected WorkspaceEntity() { }
    protected WorkspaceEntity(Guid workspaceId)
    {
        if (workspaceId == Guid.Empty) throw new ArgumentException("A workspace is required.", nameof(workspaceId));
        WorkspaceId = workspaceId;
    }
}
