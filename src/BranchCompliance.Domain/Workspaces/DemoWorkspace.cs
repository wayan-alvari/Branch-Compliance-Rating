namespace BranchCompliance.Domain.Workspaces;

public sealed class DemoWorkspace
{
    public Guid WorkspaceId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime LastActivityAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public int SeedVersion { get; private set; }
    public static readonly TimeSpan InactivityLimit = TimeSpan.FromHours(6);

    private DemoWorkspace() { }
    public DemoWorkspace(Guid workspaceId, DateTime now, int seedVersion)
    {
        if (workspaceId == Guid.Empty || now.Kind != DateTimeKind.Utc) throw new ArgumentException("A workspace ID and UTC time are required.");
        WorkspaceId = workspaceId;
        CreatedAtUtc = LastActivityAtUtc = now;
        ExpiresAtUtc = now + InactivityLimit;
        SeedVersion = seedVersion;
    }

    public bool IsExpired(DateTime now, DateTime? lastObserved = null) => now >= (lastObserved ?? LastActivityAtUtc) + InactivityLimit;

    public void Touch(DateTime now)
    {
        if (now.Kind != DateTimeKind.Utc || now < LastActivityAtUtc) throw new ArgumentException("Activity must use a monotonic UTC time.");
        LastActivityAtUtc = now;
        ExpiresAtUtc = now + InactivityLimit;
    }
}
