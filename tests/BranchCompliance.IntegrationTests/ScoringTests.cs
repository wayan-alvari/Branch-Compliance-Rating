using System.Net;
using System.Net.Http.Json;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BranchCompliance.IntegrationTests;

public sealed class ScoringTests
{
    private sealed record WorkspaceInfo(Guid WorkspaceId, Guid[] AuditIds);
    private sealed record ScenarioInfo(Guid PeriodId, Guid AssessmentId, Guid[] Criteria);

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

    private static async Task<ScenarioInfo> OpenAssessmentPhaseAsync(ComplianceWebFactory factory,
        HttpClient administrator, Guid workspace)
    {
        var scenario = await Read(factory, workspace, async db =>
        {
            var period = await db.Periods.Include(row => row.Criteria).Include(row => row.Bands)
                .AsSplitQuery().SingleAsync(row => row.Phase == PeriodPhase.SubmissionOpen);
            var assessment = await db.Assessments.Include(row => row.Responses).Include(row => row.Evidence)
                .AsSplitQuery().SingleAsync(row => row.PeriodId == period.Id && row.State == AssessmentState.InProgress);
            foreach (var criterion in period.Criteria)
            {
                var response = assessment.Responses.SingleOrDefault(row => row.PeriodCriterionId == criterion.Id)
                    ?? assessment.SaveResponse(period, criterion.Id,
                        "Synthetic completed response prepared for scoring tests.", "", "branch", factory.Clock.UtcNow);
                if (criterion.EvidenceRequired && !assessment.Evidence.Any(row => row.ResponseId == response.Id))
                    assessment.AddEvidence(period, new EvidenceFile(workspace, assessment.Id, response.Id, null,
                        "synthetic.pdf", "application/pdf", 24, new string('A', 64), "branch", factory.Clock.UtcNow),
                        "branch", factory.Clock.UtcNow);
            }
            assessment.Submit(period, "branch", factory.Clock.UtcNow);
            await db.SaveChangesAsync();
            return new ScenarioInfo(period.Id, assessment.Id,
                period.Criteria.OrderBy(row => row.Code).Select(row => row.Id).ToArray());
        });
        var advance = await ComplianceWebFactory.PostAsync(administrator, $"/Periods/Details/{scenario.PeriodId}",
            $"/Periods/Advance/{scenario.PeriodId}", new());
        Assert.Equal(HttpStatusCode.Redirect, advance.StatusCode);
        return scenario;
    }

    [Fact]
    public async Task Assigned_assessor_scores_revises_completes_and_publication_controls_visibility()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, "admin@compliance.demo");
        var workspace = await Workspace(browser);
        var scenario = await OpenAssessmentPhaseAsync(factory, browser, workspace);
        await SwitchRoleAsync(browser, "assessor@compliance.demo");

        var index = await browser.GetStringAsync("/Scoring");
        Assert.Contains("Harbor Point", index);
        Assert.Contains("Maple Junction", index);
        var detailPath = $"/Scoring/Details/{scenario.AssessmentId}";
        var detail = await browser.GetStringAsync(detailPath);
        Assert.Contains("Branch response", detail);
        Assert.Contains("synthetic-practice-evidence.pdf", detail);
        Assert.Contains("Contribution = score", detail);

        var lowWithoutNote = await ComplianceWebFactory.PostAsync(browser, detailPath,
            $"/Scoring/Save/{scenario.AssessmentId}", new()
            {
                ["CriterionId"] = scenario.Criteria[0].ToString(),
                ["Score"] = "60.00",
                ["Note"] = ""
            });
        Assert.Equal(HttpStatusCode.BadRequest, lowWithoutNote.StatusCode);
        Assert.Contains("Score note is required", await lowWithoutNote.Content.ReadAsStringAsync());
        var excessPrecision = await ComplianceWebFactory.PostAsync(browser, detailPath,
            $"/Scoring/Save/{scenario.AssessmentId}", new()
            {
                ["CriterionId"] = scenario.Criteria[0].ToString(),
                ["Score"] = "80.001",
                ["Note"] = ""
            });
        Assert.Equal(HttpStatusCode.BadRequest, excessPrecision.StatusCode);
        Assert.Contains("up to two decimal places", await excessPrecision.Content.ReadAsStringAsync());

        for (var indexOfCriterion = 0; indexOfCriterion < scenario.Criteria.Length; indexOfCriterion++)
        {
            var score = indexOfCriterion == 0 ? "60.00" : "80.00";
            var save = await ComplianceWebFactory.PostAsync(browser, detailPath,
                $"/Scoring/Save/{scenario.AssessmentId}", new()
                {
                    ["CriterionId"] = scenario.Criteria[indexOfCriterion].ToString(),
                    ["Score"] = score,
                    ["Note"] = indexOfCriterion == 0
                        ? "The fictional response needs more detail for this criterion."
                        : ""
                });
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        }
        Assert.Equal(HttpStatusCode.Redirect, (await ComplianceWebFactory.PostAsync(browser, detailPath,
            $"/Scoring/Save/{scenario.AssessmentId}", new()
            {
                ["CriterionId"] = scenario.Criteria[0].ToString(),
                ["Score"] = "90.00",
                ["Note"] = "A second review supports the revised draft score."
            })).StatusCode);
        detail = await browser.GetStringAsync(detailPath);
        Assert.Contains("81.00", detail);
        Assert.Contains("Good", detail);
        Assert.Contains("revision 2", detail);
        Assert.Equal(HttpStatusCode.Redirect,
            (await ComplianceWebFactory.PostAsync(browser, detailPath, $"/Scoring/Complete/{scenario.AssessmentId}", new())).StatusCode);

        var scored = await Read(factory, workspace, async db =>
        {
            var assessment = await db.Assessments.Include(row => row.Scores).SingleAsync(row => row.Id == scenario.AssessmentId);
            return new
            {
                assessment.State,
                assessment.ProvisionalScore,
                assessment.ProvisionalRating,
                FirstRevisions = assessment.Scores.Where(row => row.PeriodCriterionId == scenario.Criteria[0])
                    .OrderBy(row => row.Revision).Select(row => new { row.Revision, row.Value }).ToArray()
            };
        });
        Assert.Equal(AssessmentState.ProvisionallyScored, scored.State);
        Assert.Equal(81m, scored.ProvisionalScore);
        Assert.Equal("Good", scored.ProvisionalRating);
        Assert.Equal([(1, 60m), (2, 90m)], scored.FirstRevisions.Select(row => (row.Revision, row.Value)));

        await SwitchRoleAsync(browser, "branch@compliance.demo");
        var hidden = await browser.GetStringAsync("/Dashboard");
        Assert.Contains("Not published", hidden);
        Assert.DoesNotContain("81.00", hidden);
        await SwitchRoleAsync(browser, "admin@compliance.demo");
        var premature = await ComplianceWebFactory.PostAsync(browser, $"/Periods/Details/{scenario.PeriodId}",
            $"/Periods/Advance/{scenario.PeriodId}", new());
        Assert.Equal(HttpStatusCode.BadRequest, premature.StatusCode);
        Assert.Contains("Every assessment must be completely scored", await premature.Content.ReadAsStringAsync());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<WorkspaceContext>().Activate(workspace);
            var db = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();
            var period = await db.Periods.Include(row => row.Criteria).Include(row => row.Bands)
                .AsSplitQuery().SingleAsync(row => row.Id == scenario.PeriodId);
            var assessorId = await db.Assessments.Where(row => row.Id == scenario.AssessmentId)
                .Select(row => row.AssessorId).SingleAsync();
            var remaining = await db.Assessments.Include(row => row.Scores)
                .Where(row => row.PeriodId == period.Id && row.State == AssessmentState.Submitted).ToListAsync();
            foreach (var assessment in remaining)
            {
                foreach (var criterion in period.Criteria)
                    assessment.ScoreCriterion(period, criterion.Id, 85m, "", assessorId, factory.Clock.UtcNow);
                assessment.CompleteScoring(period, assessorId, factory.Clock.UtcNow);
            }
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Redirect, (await ComplianceWebFactory.PostAsync(browser,
            $"/Periods/Details/{scenario.PeriodId}", $"/Periods/Advance/{scenario.PeriodId}", new())).StatusCode);
        Assert.Equal(PeriodPhase.AppealOpen,
            await Read(factory, workspace, db => db.Periods.Where(row => row.Id == scenario.PeriodId).Select(row => row.Phase).SingleAsync()));

        await SwitchRoleAsync(browser, "branch@compliance.demo");
        var published = await browser.GetStringAsync("/Dashboard");
        Assert.Contains("81.00", published);
        Assert.Contains("Good", published);
        await SwitchRoleAsync(browser, "assessor@compliance.demo");
        Assert.Contains("Published provisional result", await browser.GetStringAsync(detailPath));
    }

    [Theory]
    [InlineData("admin@compliance.demo")]
    [InlineData("branch@compliance.demo")]
    [InlineData("approver@compliance.demo")]
    public async Task Non_assessors_cannot_read_or_post_scoring(string email)
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, email);
        var result = await browser.GetAsync("/Scoring");
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.Contains("AccessDenied", result.Headers.Location!.OriginalString);
        var crafted = await ComplianceWebFactory.PostAsync(browser, "/Dashboard", $"/Scoring/Save/{Guid.NewGuid()}", new()
        {
            ["CriterionId"] = Guid.NewGuid().ToString(),
            ["Score"] = "100.00",
            ["Note"] = ""
        });
        Assert.Equal(HttpStatusCode.Redirect, crafted.StatusCode);
        Assert.Contains("AccessDenied", crafted.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Assessor_cannot_score_another_browser_workspace()
    {
        await using var factory = new ComplianceWebFactory();
        using var first = factory.Browser();
        using var second = factory.Browser();
        await ComplianceWebFactory.LoginAsync(first, "assessor@compliance.demo");
        await ComplianceWebFactory.LoginAsync(second, "assessor@compliance.demo");
        var firstWorkspace = await Workspace(first);
        var foreign = await Read(factory, firstWorkspace, async db => new
        {
            AssessmentId = await db.Assessments.Select(row => row.Id).FirstAsync(),
            CriterionId = await db.PeriodCriteria.Select(row => row.Id).FirstAsync()
        });
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync($"/Scoring/Details/{foreign.AssessmentId}")).StatusCode);
        var post = await ComplianceWebFactory.PostAsync(second, "/Scoring", $"/Scoring/Save/{foreign.AssessmentId}", new()
        {
            ["CriterionId"] = foreign.CriterionId.ToString(),
            ["Score"] = "100.00",
            ["Note"] = ""
        });
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }
}
