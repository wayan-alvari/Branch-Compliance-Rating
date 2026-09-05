using BranchCompliance.Application.Security;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Branches;
using BranchCompliance.Domain.Periods;

namespace BranchCompliance.UnitTests;

public sealed class AccessRuleTests
{
    [Fact]
    public void Workspace_precedes_role_and_assignment_checks()
    {
        var now = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);
        var template = TemplateAndScoringTests.Template();
        template.Publish("admin", now);
        var period = new AssessmentPeriod(template.WorkspaceId, "Policy practice", template, now,
            now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(4), "admin", now);
        var branch = new Branch(template.WorkspaceId, "DEMO-HP", "Harbor Point", "Coastal", "own-branch", "admin", now);
        var assessment = new BranchAssessment(period, branch, "assigned-assessor", "admin", now);
        var workspace = template.WorkspaceId;
        Assert.True(AccessRules.CanReadAssessment(workspace, new Actor("own-branch", DemoRoles.BranchUser, "Branch User"), assessment));
        Assert.False(AccessRules.CanReadAssessment(workspace, new Actor("other-branch", DemoRoles.BranchUser, "Branch User"), assessment));
        Assert.True(AccessRules.CanReadAssessment(workspace, new Actor("assigned-assessor", DemoRoles.Assessor, "Assessor"), assessment));
        Assert.False(AccessRules.CanReadAssessment(workspace, new Actor("unassigned-assessor", DemoRoles.Assessor, "Assessor"), assessment));
        Assert.True(AccessRules.CanReadAssessment(workspace, new Actor("approver", DemoRoles.Approver, "Approver"), assessment));
        Assert.False(AccessRules.CanReadAssessment(Guid.NewGuid(), new Actor("admin", DemoRoles.Administrator, "Administrator"), assessment));
        Assert.False(AccessRules.CanReadAssessment(Guid.Empty, new Actor("admin", DemoRoles.Administrator, "Administrator"), assessment));
        Assert.False(AccessRules.CanReadAssessment(workspace, new Actor("", DemoRoles.Administrator, "Anonymous"), assessment));
        Assert.Throws<AccessDeniedException>(() => AccessRules.RequireRole(new Actor("branch", DemoRoles.BranchUser, "Branch User"), DemoRoles.Administrator));
    }
}
