using BranchCompliance.Domain.Rules;
using BranchCompliance.Domain.Workspaces;

namespace BranchCompliance.Domain.Assessments;

public enum AppealDecision { Pending, Accepted, Rejected }

public sealed class Appeal : WorkspaceEntity
{
    public Guid AssessmentId { get; private set; }
    public Guid PeriodCriterionId { get; private set; }
    public string Reason { get; private set; } = "";
    public string Clarification { get; private set; } = "";
    public decimal OriginalScore { get; private set; }
    public DateTime SubmittedAtUtc { get; private set; }
    public string SubmittedBy { get; private set; } = "";
    public AppealDecision Decision { get; private set; }
    public string DecisionNote { get; private set; } = "";
    public decimal? RevisedScore { get; private set; }
    public string? DecidedBy { get; private set; }
    public DateTime? DecidedAtUtc { get; private set; }
    private Appeal() { }
    internal Appeal(Guid workspaceId, Guid assessmentId, Guid criterionId, decimal originalScore, string reason,
        string clarification, string actorId, DateTime now) : base(workspaceId)
    {
        AssessmentId = assessmentId;
        PeriodCriterionId = criterionId;
        OriginalScore = originalScore;
        Reason = Rule.Text(reason, "Appeal reason", 2000);
        Clarification = Rule.Text(clarification, "Clarification", 2000, required: false);
        SubmittedBy = Rule.Text(actorId, "Actor", 128);
        Rule.Utc(now);
        SubmittedAtUtc = now;
    }

    internal void Decide(bool accept, string note, decimal? revisedScore, string actorId, DateTime now)
    {
        Rule.Require(Decision == AppealDecision.Pending, "This appeal has already been decided.");
        DecisionNote = Rule.Text(note, "Decision note", 2000);
        Rule.Require(!accept || revisedScore is not null, "Accepting an appeal requires an explicit revised score.");
        Rule.Require(accept || revisedScore is null, "A rejected appeal cannot revise the score.");
        if (revisedScore is not null) Rule.Percentage(revisedScore.Value, "Revised score");
        Decision = accept ? AppealDecision.Accepted : AppealDecision.Rejected;
        RevisedScore = revisedScore;
        DecidedBy = Rule.Text(actorId, "Actor", 128);
        Rule.Utc(now);
        DecidedAtUtc = now;
    }
}
