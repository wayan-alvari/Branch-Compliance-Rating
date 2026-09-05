using BranchCompliance.Application.Workspaces;

namespace BranchCompliance.IntegrationTests;

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; private set; } = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);
    public void Advance(TimeSpan interval) => UtcNow += interval;
}
