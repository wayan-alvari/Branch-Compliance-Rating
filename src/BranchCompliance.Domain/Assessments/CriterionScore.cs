using BranchCompliance.Domain.Rules;
using BranchCompliance.Domain.Workspaces;

namespace BranchCompliance.Domain.Assessments;

public sealed class CriterionScore : WorkspaceEntity
{
    public Guid AssessmentId { get; private set; }
    public Guid PeriodCriterionId { get; private set; }
    public decimal Value { get; private set; }
    public string Note { get; private set; } = "";
    public string ActorId { get; private set; } = "";
    public DateTime AtUtc { get; private set; }
    public int Revision { get; private set; }
    public Guid? AppealId { get; private set; }
    private CriterionScore() { }
    internal CriterionScore(Guid workspaceId, Guid assessmentId, Guid criterionId, decimal value, string note,
        string actorId, DateTime now, int revision, Guid? appealId = null) : base(workspaceId)
    {
        AssessmentId = assessmentId;
        PeriodCriterionId = criterionId;
        Value = Rule.Percentage(value, "Score");
        Note = Rule.Text(note, "Score note", 2000, required: value < 70m || appealId is not null);
        ActorId = Rule.Text(actorId, "Actor", 128);
        Rule.Utc(now);
        AtUtc = now;
        Revision = revision;
        AppealId = appealId;
    }
}
