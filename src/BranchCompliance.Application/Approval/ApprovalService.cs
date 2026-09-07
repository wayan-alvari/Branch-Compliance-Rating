using BranchCompliance.Application.Security;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Periods;

namespace BranchCompliance.Application.Approval;

public sealed record ApprovalPeriodItem(
    Guid PeriodId,
    string Name,
    PeriodPhase Phase,
    int PendingAppeals,
    int ReadyAssessments,
    int AssessmentCount,
    DateTime FinalizationDeadlineUtc,
    bool CanFinalize);

public sealed record ApprovalAppealRow(
    BranchAssessment Assessment,
    PeriodCriterion Criterion,
    BranchResponse? Response,
    CriterionScore OriginalScore,
    Appeal Appeal,
    IReadOnlyList<EvidenceFile> ResponseEvidence,
    IReadOnlyList<EvidenceFile> AppealEvidence,
    bool CanDecide);

public sealed record ApprovalDetails(
    AssessmentPeriod Period,
    IReadOnlyList<BranchAssessment> Assessments,
    IReadOnlyList<ApprovalAppealRow> Appeals,
    bool CanFinalize);

public interface IApprovalStore
{
    Task<IReadOnlyList<AssessmentPeriod>> PeriodsAsync(CancellationToken cancellationToken);
    Task<AssessmentPeriod?> PeriodAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<BranchAssessment>> AssessmentsAsync(Guid periodId, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}

public sealed class ApprovalService(
    IApprovalStore store,
    IWorkspaceContext workspace,
    ICurrentActor actors,
    IClock clock)
{
    public async Task<IReadOnlyList<ApprovalPeriodItem>> ListAsync(CancellationToken cancellationToken)
    {
        Approver();
        var periods = await store.PeriodsAsync(cancellationToken);
        var result = new List<ApprovalPeriodItem>();
        foreach (var period in periods.Where(row => row.Phase is PeriodPhase.AppealOpen or PeriodPhase.FinalReview or PeriodPhase.Finalized))
        {
            var assessments = await store.AssessmentsAsync(period.Id, cancellationToken);
            result.Add(new ApprovalPeriodItem(period.Id, period.Name, period.Phase,
                assessments.Sum(row => row.Appeals.Count(appeal => appeal.Decision == AppealDecision.Pending)),
                assessments.Count(row => row.State is AssessmentState.AwaitingFinalization or AssessmentState.Finalized),
                assessments.Count, period.FinalizationDeadlineUtc, CanFinalize(period, assessments)));
        }
        return result.OrderBy(row => row.Phase == PeriodPhase.Finalized).ThenBy(row => row.FinalizationDeadlineUtc).ToArray();
    }

    public async Task<ApprovalDetails> DetailsAsync(Guid periodId, CancellationToken cancellationToken)
    {
        Approver();
        var period = await RequirePeriodAsync(periodId, cancellationToken);
        if (period.Phase is not (PeriodPhase.AppealOpen or PeriodPhase.FinalReview or PeriodPhase.Finalized))
            throw new ResourceNotFoundException();
        var assessments = await store.AssessmentsAsync(periodId, cancellationToken);
        var canDecide = period.Phase is PeriodPhase.AppealOpen or PeriodPhase.FinalReview &&
                        clock.UtcNow >= period.OpensAtUtc && clock.UtcNow < period.FinalizationDeadlineUtc;
        var rows = assessments.SelectMany(assessment => assessment.Appeals.Select(appeal =>
        {
            var criterion = period.Criteria.Single(row => row.Id == appeal.PeriodCriterionId);
            var response = assessment.Responses.SingleOrDefault(row => row.PeriodCriterionId == criterion.Id);
            var original = assessment.Scores.Where(row => row.PeriodCriterionId == criterion.Id && row.AppealId is null)
                .MaxBy(row => row.Revision) ?? throw new InvalidOperationException("An appeal is missing its original score.");
            return new ApprovalAppealRow(assessment, criterion, response, original, appeal,
                response is null ? [] : assessment.Evidence.Where(row => row.ResponseId == response.Id).OrderBy(row => row.UploadedAtUtc).ToArray(),
                assessment.Evidence.Where(row => row.AppealId == appeal.Id).OrderBy(row => row.UploadedAtUtc).ToArray(),
                canDecide && appeal.Decision == AppealDecision.Pending);
        })).OrderByDescending(row => row.Appeal.Decision == AppealDecision.Pending)
            .ThenBy(row => row.Assessment.BranchName, StringComparer.Ordinal)
            .ThenBy(row => row.Criterion.Code, StringComparer.Ordinal).ToArray();
        return new ApprovalDetails(period, assessments, rows, CanFinalize(period, assessments));
    }

    public async Task DecideAsync(Guid periodId, Guid assessmentId, Guid appealId, bool accept,
        string note, decimal? revisedScore, CancellationToken cancellationToken)
    {
        var actor = Approver();
        var period = await RequirePeriodAsync(periodId, cancellationToken);
        var assessments = await store.AssessmentsAsync(periodId, cancellationToken);
        var assessment = assessments.SingleOrDefault(row => row.Id == assessmentId)
            ?? throw new ResourceNotFoundException();
        assessment.DecideAppeal(period, appealId, accept, note, revisedScore, actor.Id, clock.UtcNow);
        await store.SaveAsync(cancellationToken);
    }

    public async Task FinalizeAsync(Guid periodId, CancellationToken cancellationToken)
    {
        var actor = Approver();
        var period = await RequirePeriodAsync(periodId, cancellationToken);
        var assessments = await store.AssessmentsAsync(periodId, cancellationToken);
        period.FinalizeResults(assessments, actor.Id, clock.UtcNow);
        await store.SaveAsync(cancellationToken);
    }

    private Actor Approver()
    {
        var actor = actors.Get();
        AccessRules.RequireRole(actor, DemoRoles.Approver);
        if (workspace.WorkspaceId == Guid.Empty) throw new AccessDeniedException();
        return actor;
    }

    private async Task<AssessmentPeriod> RequirePeriodAsync(Guid id, CancellationToken cancellationToken)
        => await store.PeriodAsync(id, cancellationToken) ?? throw new ResourceNotFoundException();

    private bool CanFinalize(AssessmentPeriod period, IReadOnlyCollection<BranchAssessment> assessments)
        => period.Phase == PeriodPhase.FinalReview && assessments.Count > 0 &&
           clock.UtcNow >= period.OpensAtUtc && clock.UtcNow < period.FinalizationDeadlineUtc &&
           assessments.All(row => row.State == AssessmentState.AwaitingFinalization &&
                                  row.Appeals.All(appeal => appeal.Decision != AppealDecision.Pending));
}
