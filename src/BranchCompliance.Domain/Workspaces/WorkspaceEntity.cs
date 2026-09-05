namespace BranchCompliance.Domain.Workspaces;

public abstract class WorkspaceEntity
{
    private readonly List<AuditEvent> _pendingAudit = [];
    public IReadOnlyCollection<AuditEvent> PendingAudit => _pendingAudit.AsReadOnly();
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; protected set; }
    public int ChangeVersion { get; private set; }
    protected WorkspaceEntity() { }
    protected WorkspaceEntity(Guid workspaceId)
    {
        if (workspaceId == Guid.Empty) throw new ArgumentException("A workspace is required.", nameof(workspaceId));
        WorkspaceId = workspaceId;
    }

    protected void Record(string actorId, string action, DateTime now, string details)
    {
        Rules.Rule.Utc(now);
        ChangeVersion = checked(ChangeVersion + 1);
        _pendingAudit.Add(new AuditEvent(WorkspaceId, actorId, action, Id, now, details));
    }

    public void ClearPendingAudit() => _pendingAudit.Clear();
}
