using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Templates;
using BranchCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BranchCompliance.IntegrationTests;

public sealed class ConfigurationTests
{
    private sealed record WorkspaceInfo(Guid WorkspaceId, Guid[] AuditIds);
    private static async Task<Guid> Workspace(HttpClient browser) => (await browser.GetFromJsonAsync<WorkspaceInfo>("/test-workspace"))!.WorkspaceId;
    private static Guid RedirectId(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return Guid.Parse(Regex.Match(response.Headers.Location!.OriginalString, "[a-fA-F0-9-]{36}").Value);
    }
    private static async Task<T> Read<T>(ComplianceWebFactory factory, Guid workspace, Func<ComplianceDbContext, Task<T>> query)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<WorkspaceContext>().Activate(workspace);
        return await query(scope.ServiceProvider.GetRequiredService<ComplianceDbContext>());
    }

    [Fact]
    public async Task Administrator_configures_publishes_and_versions_a_template_through_http()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, "admin@compliance.demo");
        var workspace = await Workspace(browser);
        var templateId = RedirectId(await ComplianceWebFactory.PostAsync(browser, "/Templates/Create", "/Templates/Create",
            new() { ["Name"] = "Original practice template", ["Description"] = "A fictional configuration test." }));
        var categoryPost = await ComplianceWebFactory.PostAsync(browser, $"/Templates/Category?templateId={templateId}", "/Templates/Category",
            new() { ["TemplateId"] = templateId.ToString(), ["Name"] = "General Readiness", ["Order"] = "1" });
        Assert.Equal(HttpStatusCode.Redirect, categoryPost.StatusCode);
        var categoryId = await Read(factory, workspace, db => db.TemplateCategories.Where(row => row.TemplateId == templateId).Select(row => row.Id).SingleAsync());
        async Task<HttpResponseMessage> Criterion(string code, string weight, Guid? id = null) => await ComplianceWebFactory.PostAsync(browser,
            $"/Templates/Criterion?templateId={templateId}", "/Templates/Criterion", new()
            {
                ["TemplateId"] = templateId.ToString(),
                ["Id"] = id?.ToString() ?? "",
                ["CategoryId"] = categoryId.ToString(),
                ["Code"] = code,
                ["Title"] = "Original generic criterion",
                ["Guidance"] = "Describe a fictional example only.",
                ["Weight"] = weight,
                ["Order"] = "1",
                ["EvidenceRequired"] = "false"
            });
        Assert.Equal(HttpStatusCode.Redirect, (await Criterion("PR-01", "40.00")).StatusCode);
        var invalidPublish = await ComplianceWebFactory.PostAsync(browser, $"/Templates/Details/{templateId}", $"/Templates/Publish/{templateId}", new());
        Assert.Equal(HttpStatusCode.BadRequest, invalidPublish.StatusCode);
        Assert.Contains("exactly 100.00", await invalidPublish.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await Criterion("PR-01", "60.00")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Criterion("PR-02", "60.001")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Criterion("PR-02", "60.00")).StatusCode);
        var bands = await ComplianceWebFactory.PostAsync(browser, $"/Templates/Bands/{templateId}", "/Templates/Bands", new()
        {
            ["TemplateId"] = templateId.ToString(),
            ["Bands[0].Label"] = "Ready",
            ["Bands[0].Minimum"] = "80.00",
            ["Bands[1].Label"] = "Developing",
            ["Bands[1].Minimum"] = "0.00"
        });
        Assert.Equal(HttpStatusCode.Redirect, bands.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await ComplianceWebFactory.PostAsync(browser, $"/Templates/Details/{templateId}", $"/Templates/Publish/{templateId}", new())).StatusCode);
        var original = await Read(factory, workspace, db => db.Templates.Include(row => row.Criteria).Include(row => row.Bands).AsSplitQuery().SingleAsync(row => row.Id == templateId));
        Assert.Equal(TemplateState.Published, original.State);
        Assert.Equal(100m, original.Criteria.Sum(row => row.Weight));
        Assert.Equal(2, original.Bands.Count);
        Assert.Equal(HttpStatusCode.BadRequest, (await Criterion("PR-01", "50.00", original.Criteria.First(row => row.Code == "PR-01").Id)).StatusCode);
        var draftId = RedirectId(await ComplianceWebFactory.PostAsync(browser, $"/Templates/Details/{templateId}", $"/Templates/NewVersion/{templateId}", new()));
        var draft = await Read(factory, workspace, db => db.Templates.Include(row => row.Criteria).SingleAsync(row => row.Id == draftId));
        Assert.Equal(2, draft.Version);
        Assert.Equal(TemplateState.Draft, draft.State);
        Assert.Equal(original.FamilyId, draft.FamilyId);
        Assert.DoesNotContain(draft.Criteria.First().Id, original.Criteria.Select(row => row.Id));
        var preview = await browser.GetStringAsync($"/Templates/Details/{templateId}");
        Assert.Contains("This version is read-only", preview);
        Assert.DoesNotContain("Remove criterion", preview);
        Assert.Contains("Create new draft version", preview);
        Assert.Equal(40m, await Read(factory, workspace, db => db.TemplateCriteria.Where(row => row.TemplateId == templateId && row.Code == "PR-01").Select(row => row.Weight).SingleAsync()));
    }

    [Fact]
    public async Task Branch_configuration_preserves_historical_details_and_validates_unique_codes()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, "admin@compliance.demo");
        var workspace = await Workspace(browser);
        var create = await ComplianceWebFactory.PostAsync(browser, "/Branches/Create", "/Branches/Create", new()
        {
            ["Code"] = "DEMO-NEW",
            ["Name"] = "Cedar Example",
            ["Region"] = "Meadow",
            ["AssignDemoUser"] = "true"
        });
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        var branch = await Read(factory, workspace, db => db.Branches.SingleAsync(row => row.Code == "DEMO-NEW"));
        Assert.NotNull(branch.BranchUserId);
        var duplicate = await ComplianceWebFactory.PostAsync(browser, "/Branches/Create", "/Branches/Create", new()
        {
            ["Code"] = "demo-new",
            ["Name"] = "Duplicate example",
            ["Region"] = "Meadow"
        });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        var harbor = await Read(factory, workspace, db => db.Branches.SingleAsync(row => row.Code == "DEMO-HP"));
        var edit = await ComplianceWebFactory.PostAsync(browser, $"/Branches/Edit/{harbor.Id}", $"/Branches/Edit/{harbor.Id}", new()
        {
            ["Id"] = harbor.Id.ToString(),
            ["Code"] = harbor.Code,
            ["Name"] = "Harbor Example Revised",
            ["Region"] = "Example Region",
            ["IsActive"] = "false",
            ["AssignDemoUser"] = "false"
        });
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);
        Assert.False(await Read(factory, workspace, db => db.Branches.Where(row => row.Id == harbor.Id).Select(row => row.IsActive).SingleAsync()));
        Assert.True(await Read(factory, workspace, db => db.Assessments.Where(row => row.BranchId == harbor.Id).AllAsync(row => row.BranchName == "Harbor Point" && row.BranchUserId != null)));
        Assert.Equal(HttpStatusCode.NotFound, (await ComplianceWebFactory.PostAsync(browser, "/Branches", $"/Branches/Delete/{harbor.Id}", new())).StatusCode);
    }

    [Theory]
    [InlineData("branch@compliance.demo")]
    [InlineData("assessor@compliance.demo")]
    [InlineData("approver@compliance.demo")]
    public async Task Non_administrators_cannot_read_or_post_configuration(string email)
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, email);
        foreach (var path in new[] { "/Branches", "/Templates", "/Branches/Create", "/Templates/Create" })
        {
            var result = await browser.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
            Assert.Contains("AccessDenied", result.Headers.Location!.OriginalString);
        }
        var crafted = await ComplianceWebFactory.PostAsync(browser, "/Dashboard", "/Templates/Create", new()
        {
            ["Name"] = "Unauthorized attempt",
            ["Description"] = "Should never persist."
        });
        Assert.Equal(HttpStatusCode.Redirect, crafted.StatusCode);
        Assert.Contains("AccessDenied", crafted.Headers.Location!.OriginalString);
        var workspace = await Workspace(browser);
        Assert.False(await Read(factory, workspace, db => db.Templates.AnyAsync(row => row.Name == "Unauthorized attempt")));
    }

    [Fact]
    public async Task Administrator_cannot_read_or_modify_another_browser_template()
    {
        await using var factory = new ComplianceWebFactory();
        using var first = factory.Browser();
        using var second = factory.Browser();
        await ComplianceWebFactory.LoginAsync(first, "admin@compliance.demo");
        await ComplianceWebFactory.LoginAsync(second, "admin@compliance.demo");
        var firstWorkspace = await Workspace(first);
        var foreignId = await Read(factory, firstWorkspace, db => db.Templates.Select(row => row.Id).SingleAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync($"/Templates/Details/{foreignId}")).StatusCode);
        var update = await ComplianceWebFactory.PostAsync(second, "/Dashboard", $"/Templates/NewVersion/{foreignId}", new());
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }
}
