using BranchCompliance.Domain.Rules;
using BranchCompliance.Domain.Workspaces;

namespace BranchCompliance.Domain.Assessments;

public sealed class BranchResponse : WorkspaceEntity
{
    public Guid AssessmentId { get; private set; }
    public Guid PeriodCriterionId { get; private set; }
    public string Answer { get; private set; } = "";
    public string Comment { get; private set; } = "";
    public string UpdatedBy { get; private set; } = "";
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    private BranchResponse() { }
    internal BranchResponse(Guid workspaceId, Guid assessmentId, Guid criterionId) : base(workspaceId)
    {
        AssessmentId = assessmentId;
        PeriodCriterionId = criterionId;
    }
    internal void Update(string answer, string comment, string actorId, DateTime now)
    {
        Answer = Rule.Text(answer, "Response", 2000, required: false);
        Comment = Rule.Text(comment, "Comment", 2000, required: false);
        UpdatedBy = Rule.Text(actorId, "Actor", 128);
        Rule.Utc(now);
        UpdatedAtUtc = now;
        CompletedAtUtc = Answer.Length > 0 ? now : null;
    }
}
