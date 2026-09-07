using BranchCompliance.Application.Security;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Domain.Scoring;

namespace BranchCompliance.Application.Scoring;

public sealed record ScoringListItem(
    Guid AssessmentId,
    string BranchName,
    string PeriodName,
    PeriodPhase Phase,
    AssessmentState State,
    int ScoredCriteria,
    int CriterionCount,
    DateTime AssessmentDeadlineUtc,
    bool CanScore);

public sealed record ScoringCriterion(
    PeriodCriterion Criterion,
    BranchResponse? Response,
    IReadOnlyList<EvidenceFile> Evidence,
    CriterionScore? LatestScore,
    int RevisionCount,
    decimal? Contribution);

public sealed record ScoringDetails(
    AssessmentPeriod Period,
    BranchAssessment Assessment,
    IReadOnlyList<ScoringCriterion> Criteria,
    bool CanEdit,
    bool CanComplete,
    decimal? DraftTotal,
    string? DraftRating,
    bool ProvisionalPublished);

public interface IScoringStore
{
    Task<IReadOnlyList<BranchAssessment>> AssessmentsAsync(string assessorId, CancellationToken cancellationToken);
    Task<BranchAssessment?> AssessmentAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssessmentPeriod>> PeriodsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task<AssessmentPeriod?> PeriodAsync(Guid id, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}

public sealed class ScoringService(
    IScoringStore store,
    IWorkspaceContext workspace,
    ICurrentActor actors,
    IClock clock)
{
    public async Task<IReadOnlyList<ScoringListItem>> ListAsync(CancellationToken cancellationToken)
    {
        var actor = Assessor();
        var assessments = await store.AssessmentsAsync(actor.Id, cancellationToken);
        var periods = (await store.PeriodsAsync(assessments.Select(row => row.PeriodId).Distinct().ToArray(), cancellationToken))
            .ToDictionary(row => row.Id);
        return assessments.Select(assessment =>
        {
            var period = periods[assessment.PeriodId];
            return new ScoringListItem(assessment.Id, assessment.BranchName, period.Name, period.Phase,
                assessment.State, assessment.Scores.Select(row => row.PeriodCriterionId).Distinct().Count(),
                period.Criteria.Count, period.AssessmentDeadlineUtc, CanEdit(period, assessment));
        }).OrderByDescending(row => row.CanScore).ThenByDescending(row => row.AssessmentDeadlineUtc)
            .ThenBy(row => row.BranchName, StringComparer.Ordinal).ToArray();
    }

    public async Task<ScoringDetails> DetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = Assessor();
        var (period, assessment) = await LoadAsync(id, cancellationToken);
        RequireAssignment(actor, assessment);
        var editable = CanEdit(period, assessment);
        var rows = period.Criteria.OrderBy(row => row.CategoryOrder).ThenBy(row => row.Order).ThenBy(row => row.Code)
            .Select(criterion =>
            {
                var response = assessment.Responses.SingleOrDefault(row => row.PeriodCriterionId == criterion.Id);
                var latest = assessment.LatestScore(criterion.Id);
                return new ScoringCriterion(criterion, response,
                    response is null ? [] : assessment.Evidence.Where(row => row.ResponseId == response.Id).OrderBy(row => row.UploadedAtUtc).ToArray(),
                    latest, assessment.Scores.Count(row => row.PeriodCriterionId == criterion.Id),
                    latest is null ? null : WeightedScoring.Contribution(latest.Value, criterion.Weight));
            }).ToArray();
        var complete = rows.Length > 0 && rows.All(row => row.LatestScore is not null);
        decimal? total = complete
            ? WeightedScoring.Overall(rows.Select(row => new WeightedScore(row.LatestScore!.Value, row.Criterion.Weight)))
            : null;
        var rating = total is null ? null : WeightedScoring.Rating(total.Value,
            period.Bands.Select(row => new RatingThreshold(row.Label, row.MinimumInclusive)));
        return new ScoringDetails(period, assessment, rows, editable, editable && complete, total, rating,
            period.Phase is PeriodPhase.AppealOpen or PeriodPhase.FinalReview or PeriodPhase.Finalized);
    }

    public async Task SaveScoreAsync(Guid assessmentId, Guid criterionId, decimal score, string note,
        CancellationToken cancellationToken)
    {
        var actor = Assessor();
        var (period, assessment) = await LoadAsync(assessmentId, cancellationToken);
        RequireAssignment(actor, assessment);
        assessment.ScoreCriterion(period, criterionId, score, note, actor.Id, clock.UtcNow);
        await store.SaveAsync(cancellationToken);
    }

    public async Task CompleteAsync(Guid assessmentId, CancellationToken cancellationToken)
    {
        var actor = Assessor();
        var (period, assessment) = await LoadAsync(assessmentId, cancellationToken);
        RequireAssignment(actor, assessment);
        assessment.CompleteScoring(period, actor.Id, clock.UtcNow);
        await store.SaveAsync(cancellationToken);
    }

    private Actor Assessor()
    {
        var actor = actors.Get();
        AccessRules.RequireRole(actor, DemoRoles.Assessor);
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

    private static void RequireAssignment(Actor actor, BranchAssessment assessment)
    {
        if (assessment.AssessorId != actor.Id) throw new AccessDeniedException();
    }

    private bool CanEdit(AssessmentPeriod period, BranchAssessment assessment)
        => period.Phase == PeriodPhase.AssessmentOpen && clock.UtcNow >= period.OpensAtUtc &&
           clock.UtcNow < period.AssessmentDeadlineUtc && assessment.State == AssessmentState.Submitted;
}
