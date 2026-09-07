using System.Globalization;
using BranchCompliance.Domain.Branches;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Domain.Rules;
using BranchCompliance.Domain.Scoring;
using BranchCompliance.Domain.Workspaces;

namespace BranchCompliance.Domain.Assessments;

public enum AssessmentState { NotStarted, InProgress, Submitted, ProvisionallyScored, AppealPending, AwaitingFinalization, Finalized }

public sealed class BranchAssessment : WorkspaceEntity
{
    private readonly List<BranchResponse> _responses = [];
    private readonly List<CriterionScore> _scores = [];
    private readonly List<Appeal> _appeals = [];
    private readonly List<EvidenceFile> _evidence = [];
    public Guid PeriodId { get; private set; }
    public Guid BranchId { get; private set; }
    public string BranchName { get; private set; } = "";
    public string BranchCode { get; private set; } = "";
    public string Region { get; private set; } = "";
    public string? BranchUserId { get; private set; }
    public string AssessorId { get; private set; } = "";
    public AssessmentState State { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public DateTime? ScoredAtUtc { get; private set; }
    public DateTime? FinalizedAtUtc { get; private set; }
    public decimal? ProvisionalScore { get; private set; }
    public string? ProvisionalRating { get; private set; }
    public decimal? FinalScore { get; private set; }
    public string? FinalRating { get; private set; }
    public IReadOnlyCollection<BranchResponse> Responses => _responses.AsReadOnly();
    public IReadOnlyCollection<CriterionScore> Scores => _scores.AsReadOnly();
    public IReadOnlyCollection<Appeal> Appeals => _appeals.AsReadOnly();
    public IReadOnlyCollection<EvidenceFile> Evidence => _evidence.AsReadOnly();
    private BranchAssessment() { }

    public BranchAssessment(AssessmentPeriod period, Branch branch, string assessorId, string actorId, DateTime now) : base(period.WorkspaceId)
    {
        period.RequireAssignment(now);
        Rule.Require(branch.WorkspaceId == WorkspaceId && branch.IsActive, "Choose an active branch in this workspace.");
        PeriodId = period.Id;
        BranchId = branch.Id;
        BranchName = branch.Name;
        BranchCode = branch.Code;
        Region = branch.Region;
        BranchUserId = branch.BranchUserId;
        AssessorId = Rule.Text(assessorId, "Assigned assessor", 128);
        Record(actorId, "Branch assigned", now, "Branch and Assessor assigned to the period.");
    }

    public void Start(AssessmentPeriod period, string actorId, DateTime now)
    {
        RequirePeriod(period);
        period.RequireSubmission(now);
        Rule.Require(State == AssessmentState.NotStarted, "This assessment has already started.");
        State = AssessmentState.InProgress;
        Record(actorId, "Submission started", now, "Branch started its response.");
    }

    public BranchResponse SaveResponse(AssessmentPeriod period, Guid criterionId, string answer, string comment, string actorId, DateTime now)
    {
        RequireEditableSubmission(period, now);
        if (State == AssessmentState.NotStarted) Start(period, actorId, now);
        var criterion = RequireCriterion(period, criterionId);
        var response = _responses.SingleOrDefault(row => row.PeriodCriterionId == criterionId);
        if (response is null)
        {
            response = new BranchResponse(WorkspaceId, Id, criterionId);
            _responses.Add(response);
        }
        response.Update(answer, comment, actorId, now);
        Record(actorId, "Branch response saved", now, $"Criterion {criterion.Code}.");
        return response;
    }

    public void AddEvidence(AssessmentPeriod period, EvidenceFile file, string actorId, DateTime now)
    {
        RequirePeriod(period);
        Rule.Require(file.WorkspaceId == WorkspaceId && file.AssessmentId == Id, "Evidence must belong to this assessment.");
        if (file.ResponseId is not null)
        {
            RequireEditableSubmission(period, now);
            Rule.Require(_responses.Any(row => row.Id == file.ResponseId), "Save a response before attaching evidence.");
            Rule.Require(_evidence.Count(row => row.ResponseId == file.ResponseId) < 3, "A response allows at most three evidence files.");
        }
        else
        {
            period.RequireAppeal(now);
            Rule.Require(_appeals.Any(row => row.Id == file.AppealId && row.Decision == AppealDecision.Pending), "Evidence requires a pending appeal in this assessment.");
            Rule.Require(_evidence.Count(row => row.AppealId == file.AppealId) < 3, "An appeal allows at most three evidence files.");
        }
        _evidence.Add(file);
        Record(actorId, "Evidence attached", now, "A protected evidence file was attached.");
    }

    public EvidenceFile RemoveEvidence(AssessmentPeriod period, Guid evidenceId, string actorId, DateTime now)
    {
        RequireEditableSubmission(period, now);
        var file = _evidence.SingleOrDefault(row => row.Id == evidenceId && row.ResponseId is not null);
        Rule.Require(file is not null, "Response evidence was not found in this assessment.");
        _evidence.Remove(file!);
        Record(actorId, "Draft evidence removed", now, "An evidence attachment was removed before submission.");
        return file!;
    }

    public EvidenceFile RemoveAppealEvidence(AssessmentPeriod period, Guid evidenceId, string actorId, DateTime now)
    {
        RequirePeriod(period);
        period.RequireAppeal(now);
        var file = _evidence.SingleOrDefault(row => row.Id == evidenceId && row.AppealId is not null);
        Rule.Require(file is not null && _appeals.Any(row => row.Id == file.AppealId && row.Decision == AppealDecision.Pending),
            "Pending appeal evidence was not found in this assessment.");
        _evidence.Remove(file!);
        Record(actorId, "Appeal evidence removed", now, "A protected appeal attachment was removed before decision.");
        return file!;
    }

    public void Submit(AssessmentPeriod period, string actorId, DateTime now)
    {
        RequireEditableSubmission(period, now);
        Rule.Require(State == AssessmentState.InProgress, "Start and complete the branch responses before submitting.");
        Rule.Require(period.Criteria.All(criterion => _responses.Any(response => response.PeriodCriterionId == criterion.Id &&
            response.Answer.Length > 0 && (!criterion.EvidenceRequired || _evidence.Any(file => file.ResponseId == response.Id)))),
            "Every criterion needs a response and all required evidence before submission.");
        State = AssessmentState.Submitted;
        SubmittedAtUtc = now;
        Record(actorId, "Branch submitted", now, "All required responses and evidence are complete.");
    }

    public void ScoreCriterion(AssessmentPeriod period, Guid criterionId, decimal value, string note, string actorId, DateTime now)
    {
        RequirePeriod(period);
        period.RequireScoring(now);
        Rule.Require(State == AssessmentState.Submitted, "Only submitted assessments with unfinished scoring can be scored.");
        var criterion = RequireCriterion(period, criterionId);
        var revision = _scores.Where(row => row.PeriodCriterionId == criterionId).Select(row => row.Revision).DefaultIfEmpty().Max() + 1;
        _scores.Add(new CriterionScore(WorkspaceId, Id, criterionId, value, note, actorId, now, revision));
        Record(actorId, "Criterion scored", now, $"Criterion {criterion.Code}; revision {revision}; score {value.ToString("F2", CultureInfo.InvariantCulture)}.");
    }

    public void CompleteScoring(AssessmentPeriod period, string actorId, DateTime now)
    {
        RequirePeriod(period);
        period.RequireScoring(now);
        Rule.Require(State == AssessmentState.Submitted, "This assessment is not awaiting scoring completion.");
        ProvisionalScore = CalculateCurrentScore(period);
        ProvisionalRating = WeightedScoring.Rating(ProvisionalScore.Value, period.Bands.Select(row => new RatingThreshold(row.Label, row.MinimumInclusive)));
        ScoredAtUtc = now;
        State = AssessmentState.ProvisionallyScored;
        Record(actorId, "Scoring completed", now, "Provisional result prepared for publication.");
    }

    public Appeal SubmitAppeal(AssessmentPeriod period, Guid criterionId, string reason, string clarification, string actorId, DateTime now)
    {
        RequirePeriod(period);
        period.RequireAppeal(now);
        Rule.Require(State is AssessmentState.ProvisionallyScored or AssessmentState.AppealPending or AssessmentState.AwaitingFinalization, "A provisional result is required before appealing.");
        var criterion = RequireCriterion(period, criterionId);
        Rule.Require(!_appeals.Any(row => row.PeriodCriterionId == criterionId), "Only one appeal per criterion is allowed.");
        var score = LatestScore(criterionId);
        Rule.Require(score is not null, "This criterion has not been scored.");
        var appeal = new Appeal(WorkspaceId, Id, criterionId, score!.Value, reason, clarification, actorId, now);
        _appeals.Add(appeal);
        State = AssessmentState.AppealPending;
        Record(actorId, "Criterion appealed", now, $"Criterion {criterion.Code}.");
        return appeal;
    }

    public void DecideAppeal(AssessmentPeriod period, Guid appealId, bool accept, string note, decimal? revisedScore, string actorId, DateTime now)
    {
        RequirePeriod(period);
        period.RequireDecision(now);
        var appeal = _appeals.SingleOrDefault(row => row.Id == appealId);
        Rule.Require(appeal is not null, "Appeal not found in this assessment.");
        appeal!.Decide(accept, note, revisedScore, actorId, now);
        if (accept)
        {
            var revision = LatestScore(appeal.PeriodCriterionId)!.Revision + 1;
            _scores.Add(new CriterionScore(WorkspaceId, Id, appeal.PeriodCriterionId, revisedScore!.Value, note, actorId, now, revision, appeal.Id));
        }
        if (_appeals.All(row => row.Decision != AppealDecision.Pending)) State = AssessmentState.AwaitingFinalization;
        Record(actorId, accept ? "Appeal accepted" : "Appeal rejected", now, accept ? "A new immutable score revision was recorded." : "The original criterion score was retained.");
    }

    internal void CloseAppealWindow(AssessmentPeriod period, string actorId, DateTime now)
    {
        RequirePeriod(period);
        if (State == AssessmentState.ProvisionallyScored)
        {
            State = AssessmentState.AwaitingFinalization;
            Record(actorId, "Assessment awaiting finalization", now, "Appeal window closed without an appeal.");
        }
    }

    internal void FinalizeResult(AssessmentPeriod period, string actorId, DateTime now)
    {
        RequirePeriod(period);
        Rule.Require(period.Phase == PeriodPhase.FinalReview && State == AssessmentState.AwaitingFinalization, "This result is not ready for finalization.");
        FinalScore = CalculateCurrentScore(period);
        FinalRating = WeightedScoring.Rating(FinalScore.Value, period.Bands.Select(row => new RatingThreshold(row.Label, row.MinimumInclusive)));
        FinalizedAtUtc = now;
        State = AssessmentState.Finalized;
        Record(actorId, "Branch result finalized", now, $"Final score {FinalScore.Value.ToString("F2", CultureInfo.InvariantCulture)}.");
    }

    public CriterionScore? LatestScore(Guid criterionId) => _scores.Where(row => row.PeriodCriterionId == criterionId).MaxBy(row => row.Revision);
    public decimal CalculateCurrentScore(AssessmentPeriod period)
    {
        RequirePeriod(period);
        Rule.Require(period.Criteria.Count > 0 && period.Criteria.All(row => LatestScore(row.Id) is not null), "Every criterion must have a score.");
        return WeightedScoring.Overall(period.Criteria.Select(row => new WeightedScore(LatestScore(row.Id)!.Value, row.Weight)));
    }

    private void RequireEditableSubmission(AssessmentPeriod period, DateTime now)
    {
        RequirePeriod(period);
        period.RequireSubmission(now);
        Rule.Require(State is AssessmentState.NotStarted or AssessmentState.InProgress, "Submitted responses and evidence are read-only.");
    }
    private void RequirePeriod(AssessmentPeriod period) => Rule.Require(period.Id == PeriodId && period.WorkspaceId == WorkspaceId, "This assessment belongs to a different period or workspace.");
    private PeriodCriterion RequireCriterion(AssessmentPeriod period, Guid criterionId)
    {
        var criterion = period.Criteria.SingleOrDefault(row => row.Id == criterionId);
        Rule.Require(criterion is not null, "Criterion not found in this period snapshot.");
        return criterion!;
    }
}
