using BranchCompliance.Application.Workspaces;

namespace BranchCompliance.Infrastructure.Workspaces;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
