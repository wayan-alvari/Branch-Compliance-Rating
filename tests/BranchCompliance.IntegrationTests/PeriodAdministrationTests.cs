using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Domain.Templates;
using BranchCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BranchCompliance.IntegrationTests;

public sealed class PeriodAdministrationTests
{
    private sealed record WorkspaceInfo(Guid WorkspaceId, Guid[] AuditIds);

    private static async Task<Guid> Workspace(HttpClient browser)
        => (await browser.GetFromJsonAsync<WorkspaceInfo>("/test-workspace"))!.WorkspaceId;

    private static Guid RedirectId(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return Guid.Parse(Regex.Match(response.Headers.Location!.OriginalString, "[a-fA-F0-9-]{36}").Value);
    }

    private static async Task<T> Read<T>(ComplianceWebFactory factory, Guid workspace,
        Func<ComplianceDbContext, Task<T>> query)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<WorkspaceContext>().Activate(workspace);
        return await query(scope.ServiceProvider.GetRequiredService<ComplianceDbContext>());
    }

    private static Dictionary<string, string> PeriodFields(Guid templateId, bool valid = true) => new()
    {
        ["Name"] = "Autumn fictional review",
        ["TemplateId"] = templateId.ToString(),
        ["OpensAtUtc"] = "2026-09-05T11:00",
        ["SubmissionDeadlineUtc"] = valid ? "2026-09-12T12:00" : "2026-09-04T12:00",
        ["AssessmentDeadlineUtc"] = "2026-09-19T12:00",
        ["AppealDeadlineUtc"] = "2026-09-26T12:00",
        ["FinalizationDeadlineUtc"] = "2026-10-03T12:00"
    };

    [Fact]
    public async Task Administrator_creates_assigns_opens_and_advances_a_snapshotted_period()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, "admin@compliance.demo");
        var workspace = await Workspace(browser);
        var seed = await Read(factory, workspace, async db => new
        {
            TemplateId = await db.Templates.Where(row => row.State == TemplateState.Published).Select(row => row.Id).SingleAsync(),
            BranchId = await db.Branches.OrderBy(row => row.Name).Select(row => row.Id).FirstAsync(),
            AssessorId = await (from user in db.Users
                                join membership in db.UserRoles on user.Id equals membership.UserId
                                join role in db.Roles on membership.RoleId equals role.Id
                                where role.Name == "Assessor"
                                select user.Id).SingleAsync()
        });

        var invalid = await ComplianceWebFactory.PostAsync(browser, "/Periods/Create", "/Periods/Create", PeriodFields(seed.TemplateId, valid: false));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("strictly increasing", await invalid.Content.ReadAsStringAsync());

        var periodId = RedirectId(await ComplianceWebFactory.PostAsync(browser, "/Periods/Create", "/Periods/Create", PeriodFields(seed.TemplateId)));
        var draftPage = await browser.GetStringAsync($"/Periods/Details/{periodId}");
        Assert.Contains("Assign at least one branch", draftPage);
        Assert.Contains("will be copied when the period opens", draftPage);

        var emptyOpen = await ComplianceWebFactory.PostAsync(browser, $"/Periods/Details/{periodId}", $"/Periods/Open/{periodId}", new());
        Assert.Equal(HttpStatusCode.BadRequest, emptyOpen.StatusCode);
        Assert.Contains("Assign at least one branch", await emptyOpen.Content.ReadAsStringAsync());

        var assigned = await ComplianceWebFactory.PostAsync(browser, $"/Periods/Details/{periodId}", $"/Periods/Assign/{periodId}", new()
        {
            ["BranchId"] = seed.BranchId.ToString(),
            ["AssessorId"] = seed.AssessorId
        });
        Assert.Equal(HttpStatusCode.Redirect, assigned.StatusCode);
        var duplicate = await ComplianceWebFactory.PostAsync(browser, $"/Periods/Details/{periodId}", $"/Periods/Assign/{periodId}", new()
        {
            ["BranchId"] = seed.BranchId.ToString(),
            ["AssessorId"] = seed.AssessorId
        });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("already assigned", await duplicate.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Redirect,
            (await ComplianceWebFactory.PostAsync(browser, $"/Periods/Details/{periodId}", $"/Periods/Open/{periodId}", new())).StatusCode);
        var snapshot = await Read(factory, workspace, async db =>
        {
            var source = await db.Templates.Include(row => row.Categories).Include(row => row.Criteria).Include(row => row.Bands)
                .AsSplitQuery().SingleAsync(row => row.Id == seed.TemplateId);
            var period = await db.Periods.Include(row => row.Criteria).Include(row => row.Bands)
                .AsSplitQuery().SingleAsync(row => row.Id == periodId);
            return new
            {
                period.Phase,
                Source = source.Criteria.OrderBy(row => row.Code).Select(row => new
                {
                    SourceCriterionId = row.Id,
                    row.Code,
                    row.Title,
                    row.Guidance,
                    row.Weight,
                    row.EvidenceRequired,
                    row.Order,
                    Category = source.Categories.Single(category => category.Id == row.CategoryId).Name,
                    CategoryOrder = source.Categories.Single(category => category.Id == row.CategoryId).Order
                }).ToArray(),
                Copy = period.Criteria.OrderBy(row => row.Code).Select(row => new
                {
                    row.SourceCriterionId,
                    row.Code,
                    row.Title,
                    row.Guidance,
                    row.Weight,
                    row.EvidenceRequired,
                    row.Order,
                    row.Category,
                    row.CategoryOrder
                }).ToArray(),
                SourceBands = source.Bands.OrderBy(row => row.Order).Select(row => new { row.Label, row.MinimumInclusive }).ToArray(),
                CopyBands = period.Bands.OrderBy(row => row.Order).Select(row => new { row.Label, row.MinimumInclusive }).ToArray()
            };
        });
        Assert.Equal(PeriodPhase.SubmissionOpen, snapshot.Phase);
        Assert.Equal(snapshot.Source, snapshot.Copy);
        Assert.Equal(snapshot.SourceBands, snapshot.CopyBands);

        var earlyAdvance = await ComplianceWebFactory.PostAsync(browser, $"/Periods/Details/{periodId}", $"/Periods/Advance/{periodId}", new());
        Assert.Equal(HttpStatusCode.BadRequest, earlyAdvance.StatusCode);
        Assert.Contains("Every assigned branch must submit", await earlyAdvance.Content.ReadAsStringAsync());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<WorkspaceContext>().Activate(workspace);
            var db = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();
            var period = await db.Periods.Include(row => row.Criteria).Include(row => row.Bands).AsSplitQuery().SingleAsync(row => row.Id == periodId);
            var assessment = await db.Assessments.Include(row => row.Responses).Include(row => row.Evidence)
                .AsSplitQuery().SingleAsync(row => row.PeriodId == periodId);
            foreach (var criterion in period.Criteria)
            {
                var response = assessment.SaveResponse(period, criterion.Id, "Synthetic answer for the period administration test.", "", "branch", factory.Clock.UtcNow);
                if (criterion.EvidenceRequired)
                    assessment.AddEvidence(period, new EvidenceFile(workspace, assessment.Id, response.Id, null,
                        "synthetic.pdf", "application/pdf", 24, new string('A', 64), "branch", factory.Clock.UtcNow), "branch", factory.Clock.UtcNow);
            }
            assessment.Submit(period, "branch", factory.Clock.UtcNow);
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Redirect,
            (await ComplianceWebFactory.PostAsync(browser, $"/Periods/Details/{periodId}", $"/Periods/Advance/{periodId}", new())).StatusCode);
        Assert.Equal(PeriodPhase.AssessmentOpen,
            await Read(factory, workspace, db => db.Periods.Where(row => row.Id == periodId).Select(row => row.Phase).SingleAsync()));
        var detail = await browser.GetStringAsync($"/Periods/Details/{periodId}");
        Assert.Contains("Period-owned template snapshot", detail);
        Assert.Contains("Workplace Safety", detail);
        Assert.Contains("Publish provisional results", detail);
    }

    [Theory]
    [InlineData("branch@compliance.demo")]
    [InlineData("assessor@compliance.demo")]
    [InlineData("approver@compliance.demo")]
    public async Task Non_administrators_cannot_manage_periods(string email)
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, email);
        var result = await browser.GetAsync("/Periods");
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.Contains("AccessDenied", result.Headers.Location!.OriginalString);
        var crafted = await ComplianceWebFactory.PostAsync(browser, "/Dashboard", "/Periods/Create", PeriodFields(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Redirect, crafted.StatusCode);
        Assert.Contains("AccessDenied", crafted.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Period_routes_do_not_cross_browser_workspace_boundaries()
    {
        await using var factory = new ComplianceWebFactory();
        using var first = factory.Browser();
        using var second = factory.Browser();
        await ComplianceWebFactory.LoginAsync(first, "admin@compliance.demo");
        await ComplianceWebFactory.LoginAsync(second, "admin@compliance.demo");
        var firstWorkspace = await Workspace(first);
        var foreignId = await Read(factory, firstWorkspace, db => db.Periods.Select(row => row.Id).FirstAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync($"/Periods/Details/{foreignId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await ComplianceWebFactory.PostAsync(second, "/Dashboard", $"/Periods/Open/{foreignId}", new())).StatusCode);
    }
}
