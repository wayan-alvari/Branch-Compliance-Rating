using BranchCompliance.Application.Security;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Domain.Rules;
using BranchCompliance.Domain.Scoring;
using BranchCompliance.Domain.Workspaces;

namespace BranchCompliance.Application.Reports;

public sealed record ResultFilter(Guid? PeriodId, string Query, string Rating, AssessmentState? Status);

public sealed record ResultRow(
    Guid AssessmentId,
    string BranchCode,
    string BranchName,
    string Region,
    AssessmentState Status,
    decimal? Score,
    string? Rating,
    int? Rank,
    DateTime? FinalizedAtUtc);

public sealed record ResultRank(Guid AssessmentId, int Rank);

public sealed record ResultList(
    ResultFilter Filter,
    AssessmentPeriod SelectedPeriod,
    IReadOnlyList<AssessmentPeriod> Periods,
    IReadOnlyList<ResultRow> Rows,
    IReadOnlyList<string> Ratings);

public sealed record ResultCriterion(
    PeriodCriterion Criterion,
    CriterionScore ProvisionalScore,
    CriterionScore FinalScore,
    decimal Contribution,
    Appeal? Appeal)
{
    public decimal Effect => FinalScore.Value - ProvisionalScore.Value;
}

public sealed record ResultDetails(
    AssessmentPeriod Period,
    BranchAssessment Assessment,
    IReadOnlyList<ResultCriterion> Criteria,
    int Rank);

public sealed record AuditFilter(string Query, string Actor, DateTime? FromUtc, DateTime? ToExclusiveUtc);
public sealed record AuditRow(DateTime AtUtc, string Actor, string Action, Guid EntityId, string Details);
public sealed record AuditHistory(AuditFilter Filter, IReadOnlyList<AuditRow> Rows, bool IsLimited);

public sealed record SpreadsheetRow(
    int? Rank,
    string BranchCode,
    string BranchName,
    string Region,
    AssessmentState Status,
    decimal? Score,
    string Rating,
    DateTime? FinalizedAtUtc);

public sealed record SpreadsheetDocument(
    string PeriodName,
    DateTime GeneratedAtUtc,
    IReadOnlyList<SpreadsheetRow> Rows);

public sealed record PdfCriterionRow(
    string Category,
    string Code,
    string Title,
    decimal Weight,
    decimal ProvisionalScore,
    decimal FinalScore,
    decimal Contribution,
    string AppealEffect);

public sealed record BranchResultDocument(
    string PeriodName,
    string BranchCode,
    string BranchName,
    string Region,
    decimal ProvisionalScore,
    string ProvisionalRating,
    decimal FinalScore,
    string FinalRating,
    int Rank,
    DateTime FinalizedAtUtc,
    DateTime GeneratedAtUtc,
    IReadOnlyList<PdfCriterionRow> Criteria);

public sealed record GeneratedReport(byte[] Content, string MediaType, string FileName);

public interface IReportDocumentGenerator
{
    GeneratedReport Spreadsheet(SpreadsheetDocument document);
    GeneratedReport BranchSummary(BranchResultDocument document);
}

public interface IReportStore
{
    Task<IReadOnlyList<AssessmentPeriod>> PeriodsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<BranchAssessment>> AssessmentsAsync(Guid periodId, Actor actor,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ResultRank>> RankingAsync(Guid periodId, CancellationToken cancellationToken);
    Task<(AssessmentPeriod Period, BranchAssessment Assessment)?> DetailsAsync(Guid assessmentId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditRow>> AuditAsync(Actor actor, AuditFilter filter, int take,
        CancellationToken cancellationToken);
}

public sealed class ReportService(
    IReportStore store,
    IReportDocumentGenerator documents,
    IWorkspaceContext workspace,
    ICurrentActor currentActor,
    IClock clock)
{
    public async Task<ResultList> ListAsync(ResultFilter filter, CancellationToken cancellationToken)
    {
        var actor = Actor();
        var periods = await store.PeriodsAsync(cancellationToken);
        var selected = SelectPeriod(periods, filter.PeriodId);
        var normalized = Normalize(filter, selected.Id);
        var assessments = await store.AssessmentsAsync(selected.Id, actor, cancellationToken);
        var ranks = (await store.RankingAsync(selected.Id, cancellationToken))
            .ToDictionary(row => row.AssessmentId, row => row.Rank);
        var rows = assessments.Select(assessment => ToRow(selected, assessment, ranks))
            .Where(row => normalized.Status is null || row.Status == normalized.Status)
            .Where(row => normalized.Rating.Length == 0 || string.Equals(row.Rating, normalized.Rating, StringComparison.OrdinalIgnoreCase))
            .Where(row => normalized.Query.Length == 0 || Search(row, normalized.Query))
            .OrderBy(row => row.Rank ?? int.MaxValue)
            .ThenBy(row => row.BranchName, StringComparer.Ordinal)
            .ThenBy(row => row.AssessmentId)
            .ToArray();
        var ratings = selected.Bands.OrderByDescending(row => row.MinimumInclusive)
            .Select(row => row.Label).ToArray();
        return new ResultList(normalized, selected, periods, rows, ratings);
    }

    public async Task<ResultDetails> DetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = Actor();
        var data = await store.DetailsAsync(id, cancellationToken) ?? throw new ResourceNotFoundException();
        if (!AccessRules.CanReadAssessment(workspace.WorkspaceId, actor, data.Assessment))
            throw new AccessDeniedException();
        Rule.Require(data.Period.Phase is PeriodPhase.AppealOpen or PeriodPhase.FinalReview or PeriodPhase.Finalized &&
            data.Assessment.ProvisionalScore is not null,
            "A published result is required before viewing result details.");
        var ranking = await store.RankingAsync(data.Period.Id, cancellationToken);
        var rank = ranking.SingleOrDefault(row => row.AssessmentId == id)?.Rank ?? 0;
        var rows = data.Period.Criteria.OrderBy(row => row.CategoryOrder).ThenBy(row => row.Order)
            .Select(criterion =>
            {
                var scores = data.Assessment.Scores.Where(row => row.PeriodCriterionId == criterion.Id).ToArray();
                var provisional = scores.Where(row => row.AppealId is null).MaxBy(row => row.Revision)
                    ?? throw new InvalidOperationException("The provisional score history is incomplete.");
                var final = scores.MaxBy(row => row.Revision)!;
                var appeal = data.Assessment.Appeals.SingleOrDefault(row => row.PeriodCriterionId == criterion.Id);
                return new ResultCriterion(criterion, provisional, final,
                    WeightedScoring.Contribution(final.Value, criterion.Weight), appeal);
            }).ToArray();
        return new ResultDetails(data.Period, data.Assessment, rows, rank);
    }

    public async Task<GeneratedReport> ExportSpreadsheetAsync(ResultFilter filter,
        CancellationToken cancellationToken)
    {
        var result = await ListAsync(filter, cancellationToken);
        var rows = result.Rows.Select(row => new SpreadsheetRow(row.Rank, row.BranchCode, row.BranchName,
            row.Region, row.Status, row.Score, row.Rating ?? "", row.FinalizedAtUtc)).ToArray();
        return documents.Spreadsheet(new SpreadsheetDocument(result.SelectedPeriod.Name, clock.UtcNow, rows));
    }

    public async Task<GeneratedReport> ExportBranchSummaryAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await DetailsAsync(id, cancellationToken);
        Rule.Require(result.Assessment.State == AssessmentState.Finalized &&
            result.Assessment.ProvisionalScore is not null && result.Assessment.ProvisionalRating is not null &&
            result.Assessment.FinalScore is not null && result.Assessment.FinalRating is not null &&
            result.Assessment.FinalizedAtUtc is not null,
            "Only finalized branch results can be exported as PDF.");
        var provisionalScore = result.Assessment.ProvisionalScore
            ?? throw new InvalidOperationException("The finalized result has no provisional score.");
        var provisionalRating = result.Assessment.ProvisionalRating
            ?? throw new InvalidOperationException("The finalized result has no provisional rating.");
        var finalScore = result.Assessment.FinalScore
            ?? throw new InvalidOperationException("The finalized result has no final score.");
        var finalRating = result.Assessment.FinalRating
            ?? throw new InvalidOperationException("The finalized result has no final rating.");
        var finalizedAtUtc = result.Assessment.FinalizedAtUtc
            ?? throw new InvalidOperationException("The finalized result has no finalization time.");
        var rows = result.Criteria.Select(row => new PdfCriterionRow(row.Criterion.Category, row.Criterion.Code,
            row.Criterion.Title, row.Criterion.Weight, row.ProvisionalScore.Value, row.FinalScore.Value,
            row.Contribution, AppealEffect(row))).ToArray();
        return documents.BranchSummary(new BranchResultDocument(result.Period.Name, result.Assessment.BranchCode,
            result.Assessment.BranchName, result.Assessment.Region, provisionalScore,
            provisionalRating, finalScore, finalRating, result.Rank, finalizedAtUtc, clock.UtcNow, rows));
    }

    public async Task<AuditHistory> AuditAsync(AuditFilter filter, CancellationToken cancellationToken)
    {
        var actor = Actor();
        var normalized = Normalize(filter);
        var rows = await store.AuditAsync(actor, normalized, 201, cancellationToken);
        return new AuditHistory(normalized, rows.Take(200).ToArray(), rows.Count > 200);
    }

    private Actor Actor()
    {
        var actor = currentActor.Get();
        AccessRules.RequireRole(actor, DemoRoles.All);
        if (workspace.WorkspaceId == Guid.Empty) throw new AccessDeniedException();
        return actor;
    }

    private static AssessmentPeriod SelectPeriod(IReadOnlyList<AssessmentPeriod> periods, Guid? id)
    {
        if (periods.Count == 0) throw new ResourceNotFoundException();
        if (id is not null) return periods.SingleOrDefault(row => row.Id == id) ?? throw new ResourceNotFoundException();
        return periods.FirstOrDefault(row => row.Phase == PeriodPhase.Finalized) ?? periods[0];
    }

    private static ResultFilter Normalize(ResultFilter filter, Guid periodId)
        => new(periodId, Text(filter.Query, "Search", 120), Text(filter.Rating, "Rating", 60), filter.Status);

    private static AuditFilter Normalize(AuditFilter filter)
    {
        if (filter.FromUtc is not null) Rule.Utc(filter.FromUtc.Value);
        if (filter.ToExclusiveUtc is not null) Rule.Utc(filter.ToExclusiveUtc.Value);
        Rule.Require(filter.FromUtc is null || filter.ToExclusiveUtc is null || filter.FromUtc < filter.ToExclusiveUtc,
            "The audit start date must be before or equal to the end date.");
        return new AuditFilter(Text(filter.Query, "Search", 120), Text(filter.Actor, "Actor", 120),
            filter.FromUtc, filter.ToExclusiveUtc);
    }

    private static string Text(string? value, string name, int maximum)
        => Rule.Text(value?.Trim() ?? "", name, maximum, required: false);

    private static bool Search(ResultRow row, string query)
        => row.BranchName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
           row.BranchCode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
           row.Region.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static ResultRow ToRow(AssessmentPeriod period, BranchAssessment assessment,
        IReadOnlyDictionary<Guid, int> ranks)
    {
        var final = assessment.State == AssessmentState.Finalized && assessment.FinalScore is not null;
        var published = period.Phase is PeriodPhase.AppealOpen or PeriodPhase.FinalReview or PeriodPhase.Finalized &&
            assessment.ProvisionalScore is not null;
        return new ResultRow(assessment.Id, assessment.BranchCode, assessment.BranchName, assessment.Region,
            assessment.State, final ? assessment.FinalScore : published ? assessment.ProvisionalScore : null,
            final ? assessment.FinalRating : published ? assessment.ProvisionalRating : null,
            ranks.TryGetValue(assessment.Id, out var rank) ? rank : null, assessment.FinalizedAtUtc);
    }

    private static string AppealEffect(ResultCriterion row) => row.Appeal?.Decision switch
    {
        AppealDecision.Accepted => $"Accepted: {row.ProvisionalScore.Value:F2} to {row.FinalScore.Value:F2}",
        AppealDecision.Rejected => $"Rejected: retained {row.FinalScore.Value:F2}",
        AppealDecision.Pending => "Pending decision",
        _ => "No appeal"
    };
}
