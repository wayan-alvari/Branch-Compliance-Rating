using BranchCompliance.Domain.Workspaces;

namespace BranchCompliance.UnitTests;

public sealed class WorkspaceTests
{
    [Fact]
    public void Workspace_expires_at_exactly_six_inactive_hours_and_touch_extends_it()
    {
        var now = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);
        var workspace = new DemoWorkspace(Guid.NewGuid(), now, 1);
        Assert.False(workspace.IsExpired(now.AddHours(6).AddTicks(-1)));
        Assert.True(workspace.IsExpired(now.AddHours(6)));
        workspace.Touch(now.AddHours(5));
        Assert.False(workspace.IsExpired(now.AddHours(6)));
        Assert.True(workspace.IsExpired(now.AddHours(11)));
        Assert.Throws<ArgumentException>(() => workspace.Touch(now));
    }

    [Fact]
    public void Unpersisted_recent_activity_prevents_early_expiry()
    {
        var now = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);
        var workspace = new DemoWorkspace(Guid.NewGuid(), now, 1);
        Assert.False(workspace.IsExpired(now.AddHours(6), now.AddSeconds(30)));
        Assert.True(workspace.IsExpired(now.AddHours(6).AddSeconds(30), now.AddSeconds(30)));
    }
}
