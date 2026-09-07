using BranchCompliance.Application.Security;
using BranchCompliance.Application.Submissions;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Domain.Rules;
using BranchCompliance.Domain.Scoring;

namespace BranchCompliance.Application.Appeals;

public sealed record AppealListItem(
    Guid AssessmentId,
    string BranchName,
    string PeriodName,
    PeriodPhase Phase,
    decimal ProvisionalScore,
    string ProvisionalRating,
    int AppealCount,
    int PendingAppeals,
    DateTime AppealDeadlineUtc,
    bool CanAppeal);

public sealed record AppealCriterion(
    PeriodCriterion Criterion,
    CriterionScore Score,
    decimal Contribution,
    Appeal? Appeal,
    IReadOnlyList<EvidenceFile> Evidence);

public sealed record AppealDetails(
    AssessmentPeriod Period,
    BranchAssessment Assessment,
    IReadOnlyList<AppealCriterion> Criteria,
    bool CanAppeal);

public sealed class AppealService(
    ISubmissionStore store,
    IEvidenceStorage storage,
    IWorkspaceContext workspace,
    ICurrentActor actors,
    IClock clock)
{
    public async Task<IReadOnlyList<AppealListItem>> ListAsync(CancellationToken cancellationToken)
    {
        var actor = BranchUser();
        var assessments = await store.AssessmentsAsync(actor.Id, cancellationToken);
        var periods = (await store.PeriodsAsync(assessments.Select(row => row.PeriodId).Distinct().ToArray(), cancellationToken))
            .ToDictionary(row => row.Id);
        return assessments.Where(row => periods[row.PeriodId].Phase is PeriodPhase.AppealOpen or PeriodPhase.FinalReview)
            .Select(assessment =>
            {
                var period = periods[assessment.PeriodId];
                var provisionalScore = assessment.ProvisionalScore
                    ?? throw new InvalidOperationException("Published provisional results are incomplete.");
                var provisionalRating = assessment.ProvisionalRating
                    ?? throw new InvalidOperationException("Published provisional results are incomplete.");
                return new AppealListItem(assessment.Id, assessment.BranchName, period.Name, period.Phase,
                    provisionalScore, provisionalRating, assessment.Appeals.Count,
                    assessment.Appeals.Count(row => row.Decision == AppealDecision.Pending), period.AppealDeadlineUtc,
                    IsOpen(period));
            }).OrderByDescending(row => row.CanAppeal).ThenBy(row => row.AppealDeadlineUtc).ToArray();
    }

    public async Task<AppealDetails> DetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = BranchUser();
        var (period, assessment) = await LoadAsync(id, cancellationToken);
        RequireOwned(actor, assessment);
        Rule.Require(period.Phase is PeriodPhase.AppealOpen or PeriodPhase.FinalReview or PeriodPhase.Finalized,
            "Provisional results have not been published for this assessment.");
        Rule.Require(assessment.ProvisionalScore is not null && assessment.ProvisionalRating is not null,
            "Published provisional results are incomplete.");
        var rows = period.Criteria.OrderBy(row => row.CategoryOrder).ThenBy(row => row.Order).ThenBy(row => row.Code)
            .Select(criterion =>
            {
                var appeal = assessment.Appeals.SingleOrDefault(row => row.PeriodCriterionId == criterion.Id);
                var score = assessment.Scores
                    .Where(row => row.PeriodCriterionId == criterion.Id && row.AppealId is null)
                    .MaxBy(row => row.Revision)
                    ?? throw new InvalidOperationException("A published result is missing a criterion score.");
                return new AppealCriterion(criterion, score,
                    WeightedScoring.Contribution(score.Value, criterion.Weight), appeal,
                    appeal is null ? [] : assessment.Evidence.Where(row => row.AppealId == appeal.Id)
                        .OrderBy(row => row.UploadedAtUtc).ToArray());
            }).ToArray();
        return new AppealDetails(period, assessment, rows, IsOpen(period));
    }

    public async Task<Guid> SubmitAsync(Guid assessmentId, Guid criterionId, string reason, string clarification,
        CancellationToken cancellationToken)
    {
        var actor = BranchUser();
        var (period, assessment) = await LoadAsync(assessmentId, cancellationToken);
        RequireOwned(actor, assessment);
        var appeal = assessment.SubmitAppeal(period, criterionId, reason, clarification, actor.Id, clock.UtcNow);
        await store.SaveAsync(cancellationToken);
        return appeal.Id;
    }

    public async Task UploadEvidenceAsync(Guid assessmentId, Guid appealId, Stream content, string originalName,
        string claimedMediaType, long claimedLength, CancellationToken cancellationToken)
    {
        var actor = BranchUser();
        var (period, assessment) = await LoadAsync(assessmentId, cancellationToken);
        RequireOwned(actor, assessment);
        if (!assessment.Appeals.Any(row => row.Id == appealId && row.Decision == AppealDecision.Pending))
            throw new ResourceNotFoundException();
        var inspected = await storage.InspectAsync(content, originalName, claimedMediaType, claimedLength, cancellationToken);
        var evidence = new EvidenceFile(workspace.WorkspaceId, assessment.Id, null, appealId,
            inspected.SafeOriginalName, inspected.MediaType, inspected.Length, inspected.Sha256, actor.Id, clock.UtcNow);
        assessment.AddEvidence(period, evidence, actor.Id, clock.UtcNow);
        await storage.StoreAsync(workspace.WorkspaceId, evidence.StorageName, inspected.Content, cancellationToken);
        try { await store.SaveAsync(cancellationToken); }
        catch
        {
            try { await storage.DeleteAsync(workspace.WorkspaceId, evidence.StorageName, CancellationToken.None); }
            catch (IOException) { }
            throw;
        }
    }

    public async Task RemoveEvidenceAsync(Guid assessmentId, Guid evidenceId, CancellationToken cancellationToken)
    {
        var actor = BranchUser();
        var (period, assessment) = await LoadAsync(assessmentId, cancellationToken);
        RequireOwned(actor, assessment);
        var evidence = assessment.RemoveAppealEvidence(period, evidenceId, actor.Id, clock.UtcNow);
        store.Remove(evidence);
        await store.SaveAsync(cancellationToken);
        await storage.DeleteAsync(workspace.WorkspaceId, evidence.StorageName, cancellationToken);
    }

    private Actor BranchUser()
    {
        var actor = actors.Get();
        AccessRules.RequireRole(actor, DemoRoles.BranchUser);
        if (workspace.WorkspaceId == Guid.Empty) throw new AccessDeniedException();
        return actor;
    }

    private async Task<(AssessmentPeriod Period, BranchAssessment Assessment)> LoadAsync(Guid id,
        CancellationToken cancellationToken)
    {
        var assessment = await store.AssessmentAsync(id, cancellationToken) ?? throw new ResourceNotFoundException();
        var period = await store.PeriodAsync(assessment.PeriodId, cancellationToken) ?? throw new ResourceNotFoundException();
        return (period, assessment);
    }

    private static void RequireOwned(Actor actor, BranchAssessment assessment)
    {
        if (assessment.BranchUserId != actor.Id) throw new AccessDeniedException();
    }

    private bool IsOpen(AssessmentPeriod period)
        => period.Phase == PeriodPhase.AppealOpen && clock.UtcNow >= period.OpensAtUtc &&
           clock.UtcNow < period.AppealDeadlineUtc;
}
