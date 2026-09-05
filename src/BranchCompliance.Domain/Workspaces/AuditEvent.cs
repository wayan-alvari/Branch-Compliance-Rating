namespace BranchCompliance.Domain.Workspaces;

public sealed class AuditEvent : WorkspaceEntity
{
    public string ActorId { get; private set; } = "";
    public string Action { get; private set; } = "";
    public Guid EntityId { get; private set; }
    public DateTime AtUtc { get; private set; }
    public string Details { get; private set; } = "";
    private AuditEvent() { }

    public AuditEvent(Guid workspaceId, string actorId, string action, Guid entityId, DateTime atUtc, string details)
        : base(workspaceId)
    {
        if (string.IsNullOrWhiteSpace(actorId) || actorId.Length > 128 || string.IsNullOrWhiteSpace(action) || action.Length > 100 || details.Length > 500)
            throw new ArgumentException("Audit metadata is invalid.");
        ActorId = actorId;
        Action = action;
        EntityId = entityId;
        AtUtc = atUtc;
        Details = details;
    }
}
