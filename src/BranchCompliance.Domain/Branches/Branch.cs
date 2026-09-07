using BranchCompliance.Domain.Rules;
using BranchCompliance.Domain.Workspaces;

namespace BranchCompliance.Domain.Branches;

public sealed class Branch : WorkspaceEntity
{
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Region { get; private set; } = "";
    public string? BranchUserId { get; private set; }
    public bool IsActive { get; private set; }
    private Branch() { }

    public Branch(Guid workspaceId, string code, string name, string region, string? branchUserId, string actorId, DateTime now)
        : base(workspaceId)
    {
        Code = Rule.Text(code, "Branch code", 24).ToUpperInvariant();
        Rule.Require(Code.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'), "Branch codes use English letters, digits, and hyphens only.");
        BranchUserId = string.IsNullOrWhiteSpace(branchUserId) ? null : Rule.Text(branchUserId, "Branch user", 128);
        Edit(name, region, true, actorId, now);
    }

    public void Edit(string name, string region, bool isActive, string actorId, DateTime now)
    {
        Name = Rule.Text(name, "Branch name", 120);
        Region = Rule.Text(region, "Region label", 80);
        IsActive = isActive;
        Record(actorId, "Branch updated", now, isActive ? "Branch is active." : "Branch is inactive.");
    }

    public void AssignUser(string? userId, string actorId, DateTime now)
    {
        BranchUserId = string.IsNullOrWhiteSpace(userId) ? null : Rule.Text(userId, "Branch user", 128);
        Record(actorId, "Branch user assignment updated", now, "The assignment applies to future periods.");
    }
}
