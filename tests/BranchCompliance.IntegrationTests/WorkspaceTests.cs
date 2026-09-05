using System.Net;
using System.Net.Http.Json;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Workspaces;
using BranchCompliance.Infrastructure.Persistence;
using BranchCompliance.Infrastructure.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BranchCompliance.IntegrationTests;

public sealed class WorkspaceTests
{
    private sealed record WorkspaceInfo(Guid WorkspaceId, Guid[] AuditIds);
    private static async Task<WorkspaceInfo> Info(HttpClient browser)
        => (await browser.GetFromJsonAsync<WorkspaceInfo>("/test-workspace"))!;

    [Fact]
    public async Task Browsers_are_isolated_and_switching_roles_preserves_the_workspace()
    {
        await using var factory = new ComplianceWebFactory();
        using var first = factory.Browser();
        using var second = factory.Browser();
        await ComplianceWebFactory.LoginAsync(first, "branch@compliance.demo");
        await ComplianceWebFactory.LoginAsync(second, "branch@compliance.demo");
        var a = await Info(first);
        var b = await Info(second);
        Assert.NotEqual(a.WorkspaceId, b.WorkspaceId);
        Assert.Single(a.AuditIds);
        Assert.Single(b.AuditIds);
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync($"/test-workspace/audit/{a.AuditIds[0]}")).StatusCode);
        await ComplianceWebFactory.LoginAsync(first, "approver@compliance.demo");
        Assert.Equal(a.WorkspaceId, (await Info(first)).WorkspaceId);

        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<WorkspaceContext>().Activate(a.WorkspaceId);
        var db = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();
        db.AuditEvents.Add(new AuditEvent(b.WorkspaceId, "system", "Invalid cross-workspace attempt", Guid.NewGuid(), factory.Clock.UtcNow, "Test boundary."));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Expiry_removes_only_idle_workspace_records_and_files_and_preserves_identity()
    {
        await using var factory = new ComplianceWebFactory();
        using var first = factory.Browser();
        using var second = factory.Browser();
        await ComplianceWebFactory.LoginAsync(first, "admin@compliance.demo");
        await ComplianceWebFactory.LoginAsync(second, "admin@compliance.demo");
        var old = await Info(first);
        var other = await Info(second);
        var storage = factory.Services.GetRequiredService<WorkspaceFileStore>();
        var oldPath = storage.WorkspaceDirectory(old.WorkspaceId);
        var otherPath = storage.WorkspaceDirectory(other.WorkspaceId);
        Directory.CreateDirectory(oldPath);
        Directory.CreateDirectory(otherPath);
        await File.WriteAllTextAsync(Path.Combine(oldPath, "synthetic.txt"), "Synthetic cleanup fixture.");
        await File.WriteAllTextAsync(Path.Combine(otherPath, "synthetic.txt"), "Synthetic cleanup fixture.");
        factory.Clock.Advance(TimeSpan.FromHours(5));
        await Info(second);
        factory.Clock.Advance(TimeSpan.FromHours(1));
        var fresh = await Info(first);
        Assert.NotEqual(old.WorkspaceId, fresh.WorkspaceId);
        Assert.False(Directory.Exists(oldPath));
        Assert.True(Directory.Exists(otherPath));
        Assert.Equal(other.WorkspaceId, (await Info(second)).WorkspaceId);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();
        Assert.False(await db.Workspaces.AnyAsync(row => row.WorkspaceId == old.WorkspaceId));
        Assert.False(await db.AuditEvents.IgnoreQueryFilters().AnyAsync(row => row.WorkspaceId == old.WorkspaceId));
        Assert.Equal(4, await db.Users.CountAsync());
        Assert.Single(fresh.AuditIds);
    }

    [Fact]
    public async Task Health_and_static_requests_do_not_extend_activity_but_authenticated_visits_do()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        var health = await browser.GetAsync("/health");
        Assert.False(health.Headers.Contains("Set-Cookie"));
        await ComplianceWebFactory.LoginAsync(browser, "branch@compliance.demo");
        var original = await Info(browser);
        factory.Clock.Advance(TimeSpan.FromSeconds(30));
        await Info(browser); // The write is throttled, but observed activity must count.
        factory.Clock.Advance(TimeSpan.FromHours(6) - TimeSpan.FromSeconds(31));
        await browser.GetAsync("/health");
        await browser.GetAsync("/css/site.css");
        var stillActive = await Info(browser);
        Assert.Equal(original.WorkspaceId, stillActive.WorkspaceId);
        factory.Clock.Advance(TimeSpan.FromHours(6));
        await browser.GetAsync("/health");
        await browser.GetAsync("/css/site.css");
        Assert.NotEqual(original.WorkspaceId, (await Info(browser)).WorkspaceId);
    }

    [Fact]
    public async Task Concurrent_expired_cookie_requests_share_one_replacement_and_cleanup_is_idempotent()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, "assessor@compliance.demo");
        var original = await Info(browser);
        factory.Clock.Advance(TimeSpan.FromHours(6));
        var replacements = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Info(browser)));
        Assert.Single(replacements.Select(row => row.WorkspaceId).Distinct());
        Assert.NotEqual(original.WorkspaceId, replacements[0].WorkspaceId);
        factory.Clock.Advance(TimeSpan.FromHours(6));
        using var lease = await factory.Services.GetRequiredService<WorkspaceCoordinator>().EnterAsync(CancellationToken.None);
        await using var scope = factory.Services.CreateAsyncScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<IWorkspaceLifecycle>();
        await lifecycle.CleanupAsync(CancellationToken.None);
        await lifecycle.CleanupAsync(CancellationToken.None);
        var db = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();
        Assert.Equal(0, await db.Workspaces.CountAsync());
        Assert.Equal(0, await db.AuditEvents.IgnoreQueryFilters().CountAsync());
        Assert.Equal(4, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Disabled_demo_mode_cannot_reset_data_or_create_a_workspace_cookie()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, "admin@compliance.demo");
        var original = await Info(browser);
        factory.Services.GetRequiredService<IConfiguration>()["DemoMode:Enabled"] = "false";
        factory.Clock.Advance(TimeSpan.FromHours(7));
        using var lease = await factory.Services.GetRequiredService<WorkspaceCoordinator>().EnterAsync(CancellationToken.None);
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IWorkspaceLifecycle>().CleanupAsync(CancellationToken.None);
        var db = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();
        Assert.True(await db.Workspaces.AnyAsync(row => row.WorkspaceId == original.WorkspaceId));
        using var fresh = factory.Browser();
        var login = await fresh.GetAsync("/Account/Login");
        Assert.DoesNotContain(login.Headers.GetValues("Set-Cookie"), value => value.StartsWith("BranchCompliance.DemoWorkspaceId=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Audit_history_cannot_be_modified_or_deleted_by_normal_persistence()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, "admin@compliance.demo");
        var original = await Info(browser);
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<WorkspaceContext>().Activate(original.WorkspaceId);
        var db = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();
        var audit = await db.AuditEvents.FirstAsync();
        db.Entry(audit).Property(row => row.Action).CurrentValue = "Changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(audit).State = EntityState.Unchanged;
        db.AuditEvents.Remove(audit);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Activity_inside_database_throttle_survives_loss_of_process_memory()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, "branch@compliance.demo");
        var original = await Info(browser);
        factory.Clock.Advance(TimeSpan.FromSeconds(30));
        await Info(browser);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();
            var stored = await db.Workspaces.SingleAsync(row => row.WorkspaceId == original.WorkspaceId);
            Assert.Equal(factory.Clock.UtcNow.AddSeconds(-30), stored.LastActivityAtUtc);
        }
        factory.Services.GetRequiredService<WorkspaceCoordinator>().Forget(original.WorkspaceId);
        factory.Clock.Advance(TimeSpan.FromHours(6) - TimeSpan.FromSeconds(1));
        Assert.Equal(original.WorkspaceId, (await Info(browser)).WorkspaceId);
    }
}
