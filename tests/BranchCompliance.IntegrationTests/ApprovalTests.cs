using System.Net;
using System.Net.Http.Json;
using BranchCompliance.Application.Submissions;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Infrastructure.Persistence;
using BranchCompliance.Infrastructure.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BranchCompliance.IntegrationTests;

public sealed class ApprovalTests
{
    private sealed record WorkspaceInfo(Guid WorkspaceId, Guid[] AuditIds);
    private sealed record ScenarioInfo(Guid PeriodId, Guid AssessmentId, Guid AcceptedAppealId,
        Guid RejectedAppealId, Guid AppealEvidenceId);

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

    private static async Task<ScenarioInfo> PrepareFinalReviewAsync(ComplianceWebFactory factory, Guid workspace)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<WorkspaceContext>().Activate(workspace);
        var db = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IEvidenceStorage>();
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
                    "Synthetic completed response for approval review.", "Synthetic response comment.",
                    branchAssessment.BranchUserId!, factory.Clock.UtcNow);
            if (criterion.EvidenceRequired && !branchAssessment.Evidence.Any(row => row.ResponseId == response.Id))
                branchAssessment.AddEvidence(period, new EvidenceFile(workspace, branchAssessment.Id, response.Id, null,
                    "synthetic.pdf", "application/pdf", 24, new string('A', 64), branchAssessment.BranchUserId!, factory.Clock.UtcNow),
                    branchAssessment.BranchUserId!, factory.Clock.UtcNow);
        }
        branchAssessment.Submit(period, branchAssessment.BranchUserId!, factory.Clock.UtcNow);
        period.Advance(assessments, "admin", factory.Clock.UtcNow);
        var ordered = period.Criteria.OrderBy(row => row.Code).ToArray();
        decimal[] otherScores = [90m, 90m, 70m, 60m];
        var otherIndex = 0;
        foreach (var assessment in assessments)
        {
            var value = assessment == branchAssessment ? 80m : otherScores[otherIndex++];
            foreach (var criterion in ordered)
                assessment.ScoreCriterion(period, criterion.Id, value,
                    value < 70m ? "The fictional response supports a score below 70.00." : "",
                    assessment.AssessorId, factory.Clock.UtcNow);
            assessment.CompleteScoring(period, assessment.AssessorId, factory.Clock.UtcNow);
        }
        period.Advance(assessments, "admin", factory.Clock.UtcNow);
        var accepted = branchAssessment.SubmitAppeal(period, period.Criteria.Single(row => row.Code == "WS-01").Id,
            "Please consider the additional fictional walkway explanation.",
            "The branch describes a second generic readiness check.", branchAssessment.BranchUserId!, factory.Clock.UtcNow);
        var rejected = branchAssessment.SubmitAppeal(period, period.Criteria.Single(row => row.Code == "CS-01").Id,
            "Please recheck the fictional service-direction response.",
            "No additional example is available.", branchAssessment.BranchUserId!, factory.Clock.UtcNow);
        var bytes = SyntheticEvidence.Pdf();
        var appealEvidence = new EvidenceFile(workspace, branchAssessment.Id, null, accepted.Id,
            "approval-appeal-evidence.pdf", "application/pdf", bytes.Length,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)), branchAssessment.BranchUserId!, factory.Clock.UtcNow);
        branchAssessment.AddEvidence(period, appealEvidence, branchAssessment.BranchUserId!, factory.Clock.UtcNow);
        await storage.StoreAsync(workspace, appealEvidence.StorageName, bytes, default);
        period.Advance(assessments, "admin", factory.Clock.UtcNow);
        await db.SaveChangesAsync();
        return new ScenarioInfo(period.Id, branchAssessment.Id, accepted.Id, rejected.Id, appealEvidence.Id);
    }

    [Fact]
    public async Task Approver_decides_appeals_and_finalizes_recomputed_immutable_results()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, "admin@compliance.demo");
        var workspace = await Workspace(browser);
        var scenario = await PrepareFinalReviewAsync(factory, workspace);
        await SwitchRoleAsync(browser, "approver@compliance.demo");

        var index = await browser.GetStringAsync("/Approvals");
        Assert.Contains("Current practice cycle", index);
        Assert.Contains(">2<", index);
        var detailsPath = $"/Approvals/Details/{scenario.PeriodId}";
        var details = await browser.GetStringAsync(detailsPath);
        Assert.Contains("Original assessment record", details);
        Assert.Contains("Branch appeal", details);
        Assert.Contains("Synthetic response comment", details);
        Assert.Contains("synthetic-practice-evidence.pdf", details);
        Assert.Contains("approval-appeal-evidence.pdf", details);
        Assert.Contains("80.00", details);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync($"/Evidence/Download/{scenario.AppealEvidenceId}")).StatusCode);

        var premature = await ComplianceWebFactory.PostAsync(browser, detailsPath,
            $"/Approvals/Finalize/{scenario.PeriodId}", new());
        Assert.Equal(HttpStatusCode.BadRequest, premature.StatusCode);
        Assert.Contains("all appeals decided", await premature.Content.ReadAsStringAsync());
        var rejectWithRevision = await ComplianceWebFactory.PostAsync(browser, detailsPath,
            $"/Approvals/Decide/{scenario.PeriodId}", new()
            {
                ["AssessmentId"] = scenario.AssessmentId.ToString(),
                ["AppealId"] = scenario.RejectedAppealId.ToString(),
                ["Decision"] = "Reject",
                ["Note"] = "The original fictional score remains supported.",
                ["RevisedScore"] = "90.00"
            });
        Assert.Equal(HttpStatusCode.BadRequest, rejectWithRevision.StatusCode);
        Assert.Contains("cannot revise", await rejectWithRevision.Content.ReadAsStringAsync());
        var acceptWithoutRevision = await ComplianceWebFactory.PostAsync(browser, detailsPath,
            $"/Approvals/Decide/{scenario.PeriodId}", new()
            {
                ["AssessmentId"] = scenario.AssessmentId.ToString(),
                ["AppealId"] = scenario.AcceptedAppealId.ToString(),
                ["Decision"] = "Accept",
                ["Note"] = "The additional explanation is persuasive.",
                ["RevisedScore"] = ""
            });
        Assert.Equal(HttpStatusCode.BadRequest, acceptWithoutRevision.StatusCode);
        Assert.Contains("requires an explicit revised score", await acceptWithoutRevision.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Redirect, (await ComplianceWebFactory.PostAsync(browser, detailsPath,
            $"/Approvals/Decide/{scenario.PeriodId}", new()
            {
                ["AssessmentId"] = scenario.AssessmentId.ToString(),
                ["AppealId"] = scenario.AcceptedAppealId.ToString(),
                ["Decision"] = "Accept",
                ["Note"] = "The additional fictional explanation supports the revised score.",
                ["RevisedScore"] = "100.00"
            })).StatusCode);
        var acceptedState = await Read(factory, workspace, async db =>
        {
            var appeal = await db.Appeals.SingleAsync(row => row.Id == scenario.AcceptedAppealId);
            var revision = await db.Scores.SingleAsync(row => row.AppealId == appeal.Id);
            var assessment = await db.Assessments.SingleAsync(row => row.Id == scenario.AssessmentId);
            return new { appeal.Decision, appeal.RevisedScore, revision.Revision, revision.Value, assessment.State, assessment.ProvisionalScore };
        });
        Assert.Equal(AppealDecision.Accepted, acceptedState.Decision);
        Assert.Equal(100m, acceptedState.RevisedScore);
        Assert.Equal(2, acceptedState.Revision);
        Assert.Equal(100m, acceptedState.Value);
        Assert.Equal(AssessmentState.AppealPending, acceptedState.State);
        Assert.Equal(80m, acceptedState.ProvisionalScore);
        var duplicate = await ComplianceWebFactory.PostAsync(browser, detailsPath,
            $"/Approvals/Decide/{scenario.PeriodId}", new()
            {
                ["AssessmentId"] = scenario.AssessmentId.ToString(),
                ["AppealId"] = scenario.AcceptedAppealId.ToString(),
                ["Decision"] = "Reject",
                ["Note"] = "A second decision must not be recorded.",
                ["RevisedScore"] = ""
            });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("already been decided", await duplicate.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await ComplianceWebFactory.PostAsync(browser, detailsPath,
            $"/Approvals/Decide/{scenario.PeriodId}", new()
            {
                ["AssessmentId"] = scenario.AssessmentId.ToString(),
                ["AppealId"] = scenario.RejectedAppealId.ToString(),
                ["Decision"] = "Reject",
                ["Note"] = "The existing score remains supported by the fictional response.",
                ["RevisedScore"] = ""
            })).StatusCode);
        Assert.Equal(AssessmentState.AwaitingFinalization,
            await Read(factory, workspace, db => db.Assessments.Where(row => row.Id == scenario.AssessmentId).Select(row => row.State).SingleAsync()));

        Assert.Equal(HttpStatusCode.Redirect, (await ComplianceWebFactory.PostAsync(browser, detailsPath,
            $"/Approvals/Finalize/{scenario.PeriodId}", new())).StatusCode);
        var final = await Read(factory, workspace, async db =>
        {
            var period = await db.Periods.SingleAsync(row => row.Id == scenario.PeriodId);
            var assessment = await db.Assessments.SingleAsync(row => row.Id == scenario.AssessmentId);
            var states = await db.Assessments.Where(row => row.PeriodId == scenario.PeriodId).Select(row => row.State).ToArrayAsync();
            return new { period.Phase, assessment.ProvisionalScore, assessment.FinalScore, assessment.FinalRating, States = states };
        });
        Assert.Equal(PeriodPhase.Finalized, final.Phase);
        Assert.Equal(80m, final.ProvisionalScore);
        Assert.Equal(82m, final.FinalScore);
        Assert.Equal("Good", final.FinalRating);
        Assert.All(final.States, state => Assert.Equal(AssessmentState.Finalized, state));
        Assert.True(await Read(factory, workspace, db => db.AuditEvents.AnyAsync(row =>
            row.EntityId == scenario.PeriodId && row.Action == "Period finalized")));
        Assert.Contains("82.00", await browser.GetStringAsync(detailsPath));
        Assert.Equal(HttpStatusCode.BadRequest, (await ComplianceWebFactory.PostAsync(browser, detailsPath,
            $"/Approvals/Finalize/{scenario.PeriodId}", new())).StatusCode);

        await SwitchRoleAsync(browser, "branch@compliance.demo");
        var branchDashboard = await browser.GetStringAsync("/Dashboard");
        Assert.Contains("82.00", branchDashboard);
        var branchResult = await browser.GetStringAsync($"/Appeals/Details/{scenario.AssessmentId}");
        Assert.Contains("Final result", branchResult);
        Assert.Contains("82.00", branchResult);
        Assert.Contains("Provisional 80.00", branchResult);
    }

    [Theory]
    [InlineData("admin@compliance.demo")]
    [InlineData("branch@compliance.demo")]
    [InlineData("assessor@compliance.demo")]
    public async Task Non_approvers_cannot_read_or_post_decisions(string email)
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, email);
        var result = await browser.GetAsync("/Approvals");
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.Contains("AccessDenied", result.Headers.Location!.OriginalString);
        var crafted = await ComplianceWebFactory.PostAsync(browser, "/Dashboard", $"/Approvals/Finalize/{Guid.NewGuid()}", new());
        Assert.Equal(HttpStatusCode.Redirect, crafted.StatusCode);
        Assert.Contains("AccessDenied", crafted.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Approver_cannot_finalize_another_browser_workspace()
    {
        await using var factory = new ComplianceWebFactory();
        using var first = factory.Browser();
        using var second = factory.Browser();
        await ComplianceWebFactory.LoginAsync(first, "approver@compliance.demo");
        await ComplianceWebFactory.LoginAsync(second, "approver@compliance.demo");
        var firstWorkspace = await Workspace(first);
        var foreignPeriod = await Read(factory, firstWorkspace, db => db.Periods.Select(row => row.Id).FirstAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync($"/Approvals/Details/{foreignPeriod}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ComplianceWebFactory.PostAsync(second, "/Approvals",
            $"/Approvals/Finalize/{foreignPeriod}", new())).StatusCode);
    }
}
