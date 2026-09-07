using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Infrastructure.Persistence;
using BranchCompliance.Infrastructure.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BranchCompliance.IntegrationTests;

public sealed class AppealTests
{
    private sealed record WorkspaceInfo(Guid WorkspaceId, Guid[] AuditIds);
    private sealed record ScenarioInfo(Guid PeriodId, Guid AssessmentId, Guid CriterionId);

    private static async Task<Guid> Workspace(HttpClient browser)
        => (await browser.GetFromJsonAsync<WorkspaceInfo>("/test-workspace"))!.WorkspaceId;

    private static async Task<T> Read<T>(ComplianceWebFactory factory, Guid workspace,
        Func<ComplianceDbContext, Task<T>> query)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<WorkspaceContext>().Activate(workspace);
        return await query(scope.ServiceProvider.GetRequiredService<ComplianceDbContext>());
    }

    private static async Task SwitchRoleAsync(HttpClient browser, string email)
    {
        Assert.Equal(HttpStatusCode.Redirect,
            (await ComplianceWebFactory.PostAsync(browser, "/Dashboard", "/Account/Logout", new())).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await ComplianceWebFactory.LoginAsync(browser, email)).StatusCode);
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient browser, string formPage, Guid assessmentId,
        Guid appealId, byte[] content, string name, string mediaType)
    {
        using var body = new MultipartFormDataContent();
        body.Add(new StringContent(await ComplianceWebFactory.TokenAsync(browser, formPage)), "__RequestVerificationToken");
        body.Add(new StringContent(appealId.ToString()), "appealId");
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
        body.Add(file, "file", name);
        return await browser.PostAsync($"/Appeals/Upload/{assessmentId}", body);
    }

    private static async Task<ScenarioInfo> PublishProvisionalResultsAsync(ComplianceWebFactory factory,
        Guid workspace)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<WorkspaceContext>().Activate(workspace);
        var db = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();
        var period = await db.Periods.Include(row => row.Criteria).Include(row => row.Bands)
            .AsSplitQuery().SingleAsync(row => row.Phase == PeriodPhase.SubmissionOpen);
        var assessments = await db.Assessments.Where(row => row.PeriodId == period.Id)
            .Include(row => row.Responses).Include(row => row.Evidence).Include(row => row.Scores).Include(row => row.Appeals)
            .AsSplitQuery().OrderBy(row => row.BranchName).ToListAsync();
        var branchAssessment = assessments.Single(row => row.BranchUserId is not null);
        foreach (var criterion in period.Criteria)
        {
            var response = branchAssessment.Responses.SingleOrDefault(row => row.PeriodCriterionId == criterion.Id)
                ?? branchAssessment.SaveResponse(period, criterion.Id,
                    "Synthetic complete response for the appeal workflow.", "", "branch", factory.Clock.UtcNow);
            if (criterion.EvidenceRequired && !branchAssessment.Evidence.Any(row => row.ResponseId == response.Id))
                branchAssessment.AddEvidence(period, new EvidenceFile(workspace, branchAssessment.Id, response.Id, null,
                    "synthetic.pdf", "application/pdf", 24, new string('A', 64), "branch", factory.Clock.UtcNow),
                    "branch", factory.Clock.UtcNow);
        }
        branchAssessment.Submit(period, "branch", factory.Clock.UtcNow);
        period.Advance(assessments, "admin", factory.Clock.UtcNow);
        var orderedCriteria = period.Criteria.OrderBy(row => row.Code).ToArray();
        foreach (var assessment in assessments)
        {
            for (var index = 0; index < orderedCriteria.Length; index++)
            {
                var value = assessment == branchAssessment ? index == 0 ? 60m : 80m : 85m;
                assessment.ScoreCriterion(period, orderedCriteria[index].Id, value,
                    value < 70m ? "The fictional response needs more detail." : "", assessment.AssessorId, factory.Clock.UtcNow);
            }
            assessment.CompleteScoring(period, assessment.AssessorId, factory.Clock.UtcNow);
        }
        period.Advance(assessments, "admin", factory.Clock.UtcNow);
        await db.SaveChangesAsync();
        return new ScenarioInfo(period.Id, branchAssessment.Id, orderedCriteria[0].Id);
    }

    [Fact]
    public async Task Branch_user_reviews_published_scores_and_submits_one_evidenced_appeal_per_criterion()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, "admin@compliance.demo");
        var workspace = await Workspace(browser);
        var scenario = await PublishProvisionalResultsAsync(factory, workspace);
        await SwitchRoleAsync(browser, "branch@compliance.demo");

        var dashboard = await browser.GetStringAsync("/Dashboard");
        Assert.Contains("78.00", dashboard);
        Assert.Contains($"/Appeals/Details/{scenario.AssessmentId}", dashboard);
        var index = await browser.GetStringAsync("/Appeals");
        Assert.Contains("Harbor Point", index);
        Assert.DoesNotContain("Maple Junction", index);
        var detailPath = $"/Appeals/Details/{scenario.AssessmentId}";
        var detail = await browser.GetStringAsync(detailPath);
        Assert.Contains("Published provisional result", detail);
        Assert.Contains("78.00", detail);
        Assert.Contains("60.00", detail);
        Assert.Contains("Overall = sum(score", detail);

        var missingReason = await ComplianceWebFactory.PostAsync(browser, detailPath,
            $"/Appeals/Create/{scenario.AssessmentId}", new()
            {
                ["CriterionId"] = scenario.CriterionId.ToString(),
                ["Reason"] = "",
                ["Clarification"] = "Optional context cannot replace a reason."
            });
        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        var created = await ComplianceWebFactory.PostAsync(browser, detailPath,
            $"/Appeals/Create/{scenario.AssessmentId}", new()
            {
                ["CriterionId"] = scenario.CriterionId.ToString(),
                ["Reason"] = "Please review the score using the additional fictional explanation.",
                ["Clarification"] = "A second generic example describes the same practice check."
            });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var appeal = await Read(factory, workspace, db => db.Appeals.SingleAsync(row => row.AssessmentId == scenario.AssessmentId));
        Assert.Equal(60m, appeal.OriginalScore);
        Assert.Equal(AppealDecision.Pending, appeal.Decision);
        Assert.Equal(AssessmentState.AppealPending,
            await Read(factory, workspace, db => db.Assessments.Where(row => row.Id == scenario.AssessmentId).Select(row => row.State).SingleAsync()));
        Assert.True(await Read(factory, workspace, db => db.AuditEvents.AnyAsync(row =>
            row.EntityId == scenario.AssessmentId && row.Action == "Criterion appealed")));
        var duplicate = await ComplianceWebFactory.PostAsync(browser, detailPath,
            $"/Appeals/Create/{scenario.AssessmentId}", new()
            {
                ["CriterionId"] = scenario.CriterionId.ToString(),
                ["Reason"] = "A duplicate should be rejected.",
                ["Clarification"] = ""
            });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("Only one appeal per criterion", await duplicate.Content.ReadAsStringAsync());

        var pdf = SyntheticEvidence.Pdf();
        Assert.Equal(HttpStatusCode.Redirect, (await UploadAsync(browser, detailPath, scenario.AssessmentId,
            appeal.Id, pdf, "../../appeal context.pdf", "application/pdf")).StatusCode);
        var evidence = await Read(factory, workspace, db => db.EvidenceFiles.SingleAsync(row => row.AppealId == appeal.Id));
        Assert.Equal("appeal context.pdf", evidence.OriginalName);
        var fileStore = factory.Services.GetRequiredService<WorkspaceFileStore>();
        var path = Path.Combine(fileStore.WorkspaceDirectory(workspace), evidence.StorageName);
        Assert.True(File.Exists(path));
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/Evidence/Download/{evidence.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await ComplianceWebFactory.PostAsync(browser, detailPath,
            $"/Appeals/RemoveEvidence/{scenario.AssessmentId}", new() { ["evidenceId"] = evidence.Id.ToString() })).StatusCode);
        Assert.False(File.Exists(path));
        Assert.False(await Read(factory, workspace, db => db.EvidenceFiles.AnyAsync(row => row.Id == evidence.Id)));
        Assert.Equal(HttpStatusCode.Redirect, (await UploadAsync(browser, detailPath, scenario.AssessmentId,
            appeal.Id, pdf, "appeal-replacement.pdf", "application/pdf")).StatusCode);
        var retained = await Read(factory, workspace, db => db.EvidenceFiles.SingleAsync(row => row.AppealId == appeal.Id));

        await SwitchRoleAsync(browser, "approver@compliance.demo");
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/Evidence/Download/{retained.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/Appeals")).StatusCode);
        await SwitchRoleAsync(browser, "assessor@compliance.demo");
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/Evidence/Download/{retained.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/Appeals")).StatusCode);
    }

    [Fact]
    public async Task Unpublished_current_result_is_not_available_through_appeal_routes()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, "branch@compliance.demo");
        var workspace = await Workspace(browser);
        var currentId = await Read(factory, workspace, async db =>
        {
            var periodId = await db.Periods.Where(row => row.Phase == PeriodPhase.SubmissionOpen).Select(row => row.Id).SingleAsync();
            return await db.Assessments.Where(row => row.PeriodId == periodId && row.BranchUserId != null)
                .Select(row => row.Id).SingleAsync();
        });
        Assert.DoesNotContain("Current practice cycle", await browser.GetStringAsync("/Appeals"));
        var result = await browser.GetAsync($"/Appeals/Details/{currentId}");
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Contains("not been published", await result.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Appeal_routes_do_not_cross_browser_workspace_boundaries()
    {
        await using var factory = new ComplianceWebFactory();
        using var first = factory.Browser();
        using var second = factory.Browser();
        await ComplianceWebFactory.LoginAsync(first, "branch@compliance.demo");
        await ComplianceWebFactory.LoginAsync(second, "branch@compliance.demo");
        var firstWorkspace = await Workspace(first);
        var foreignId = await Read(factory, firstWorkspace, db => db.Assessments.Where(row => row.BranchUserId != null)
            .Select(row => row.Id).FirstAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync($"/Appeals/Details/{foreignId}")).StatusCode);
        var post = await ComplianceWebFactory.PostAsync(second, "/Dashboard", $"/Appeals/Create/{foreignId}", new()
        {
            ["CriterionId"] = Guid.NewGuid().ToString(),
            ["Reason"] = "Cross-workspace attempt.",
            ["Clarification"] = ""
        });
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }

    [Theory]
    [InlineData("admin@compliance.demo")]
    [InlineData("assessor@compliance.demo")]
    [InlineData("approver@compliance.demo")]
    public async Task Non_branch_roles_cannot_read_or_submit_appeals(string email)
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, email);
        var result = await browser.GetAsync("/Appeals");
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.Contains("AccessDenied", result.Headers.Location!.OriginalString);
        var crafted = await ComplianceWebFactory.PostAsync(browser, "/Dashboard", $"/Appeals/Create/{Guid.NewGuid()}", new()
        {
            ["CriterionId"] = Guid.NewGuid().ToString(),
            ["Reason"] = "Unauthorized appeal attempt.",
            ["Clarification"] = ""
        });
        Assert.Equal(HttpStatusCode.Redirect, crafted.StatusCode);
        Assert.Contains("AccessDenied", crafted.Headers.Location!.OriginalString);
    }
}
