namespace BranchCompliance.Infrastructure.Workspaces;

// Opaque retirement metadata lets concurrent requests carrying the same expired
// cookie converge on one replacement. It contains no assessment or identity data.
public sealed class WorkspaceRedirect
{
    public Guid WorkspaceId { get; set; }
    public Guid ReplacementWorkspaceId { get; set; }
    public DateTime RetiredAtUtc { get; set; }
}
