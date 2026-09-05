using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BranchCompliance.Application.Dashboard;
using BranchCompliance.Application.Security;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BranchCompliance.IntegrationTests;

public sealed class DashboardTests
{
    private sealed record WorkspaceInfo(Guid WorkspaceId, Guid[] AuditIds);

    [Theory]
    [InlineData("admin@compliance.demo", "Guide the assessment cycle", 5)]
    [InlineData("branch@compliance.demo", "Your branch, your next step", 1)]
    [InlineData("assessor@compliance.demo", "Review evidence with confidence", 5)]
    [InlineData("approver@compliance.demo", "Appeals &amp; final review", 5)]
    public async Task Four_role_dashboards_render_only_authorized_assignments(string email, string title, int rows)
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, email);
        var html = await browser.GetStringAsync("/Dashboard");
        Assert.Contains(title, html);
        Assert.Equal(rows, Regex.Matches(html, "data-assessment-id=").Count);
        Assert.Contains("Current practice cycle", html);
        Assert.Contains("Submissions open", html);
        Assert.Contains("Submission deadline", html);
        Assert.Contains("Not published", html);
        Assert.Contains("Recent activity", html);
        Assert.Contains("<progress", html);
        Assert.Contains("<meter", html);
        if (email == "branch@compliance.demo")
        {
            Assert.Contains("Harbor Point", html);
            Assert.Contains("1 / 10 complete", html);
            Assert.Contains("88.20", html);
            Assert.Contains("Rank 3", html);
            Assert.DoesNotContain("Maple Junction", html);
            Assert.DoesNotContain("Northfield", html);
            Assert.DoesNotContain("Riverside", html);
            Assert.DoesNotContain("Summit Square", html);
        }
        else Assert.Contains("4 of 5 branches submitted", html);
    }

    [Fact]
    public async Task Dashboard_queries_enforce_workspace_role_and_assessor_assignment_with_empty_states()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, "admin@compliance.demo");
        var workspaceId = (await browser.GetFromJsonAsync<WorkspaceInfo>("/test-workspace"))!.WorkspaceId;
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<WorkspaceContext>().Activate(workspaceId);
        var store = scope.ServiceProvider.GetRequiredService<IDashboardStore>();
        var db = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();
        var branchUser = await db.Users.Where(row => row.Email == "branch@compliance.demo").Select(row => row.Id).SingleAsync();
        var branch = await store.ReadAsync(workspaceId, new Actor(branchUser, DemoRoles.BranchUser, "Branch User"), CancellationToken.None);
        Assert.Single(branch.Queue);
        Assert.Single(branch.History);
        Assert.Null(branch.Queue[0].VisibleScore);
        Assert.Null(branch.Queue[0].VisibleRating);
        var unassigned = await store.ReadAsync(workspaceId, new Actor("unassigned-example", DemoRoles.Assessor, "Assessor"), CancellationToken.None);
        Assert.Empty(unassigned.Queue);
        Assert.Empty(unassigned.History);
        Assert.Empty(unassigned.Activity);
        Assert.Null(unassigned.Period);
        await Assert.ThrowsAsync<AccessDeniedException>(() => store.ReadAsync(Guid.NewGuid(), new Actor("admin", DemoRoles.Administrator, "Administrator"), CancellationToken.None));
        await Assert.ThrowsAsync<AccessDeniedException>(() => store.ReadAsync(workspaceId, new Actor("unknown", "UnknownRole", "Unknown"), CancellationToken.None));
    }
}
