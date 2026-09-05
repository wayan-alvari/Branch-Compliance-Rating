using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Rules;
using BranchCompliance.Domain.Templates;
using BranchCompliance.Domain.Workspaces;

namespace BranchCompliance.Domain.Periods;

public enum PeriodPhase { Draft, SubmissionOpen, AssessmentOpen, AppealOpen, FinalReview, Finalized }

public sealed class AssessmentPeriod : WorkspaceEntity
{
    private readonly List<PeriodCriterion> _criteria = [];
    private readonly List<PeriodRatingBand> _bands = [];
    public string Name { get; private set; } = "";
    public Guid TemplateId { get; private set; }
    public string TemplateName { get; private set; } = "";
    public int TemplateVersion { get; private set; }
    public DateTime OpensAtUtc { get; private set; }
    public DateTime SubmissionDeadlineUtc { get; private set; }
    public DateTime AssessmentDeadlineUtc { get; private set; }
    public DateTime AppealDeadlineUtc { get; private set; }
    public DateTime FinalizationDeadlineUtc { get; private set; }
    public PeriodPhase Phase { get; private set; }
    public IReadOnlyCollection<PeriodCriterion> Criteria => _criteria.AsReadOnly();
    public IReadOnlyCollection<PeriodRatingBand> Bands => _bands.AsReadOnly();
    private AssessmentPeriod() { }

    public AssessmentPeriod(Guid workspaceId, string name, AssessmentTemplate template, DateTime opensAtUtc,
        DateTime submissionDeadlineUtc, DateTime assessmentDeadlineUtc, DateTime appealDeadlineUtc,
        DateTime finalizationDeadlineUtc, string actorId, DateTime now) : base(workspaceId)
    {
        Rule.Require(template.WorkspaceId == workspaceId && template.State == TemplateState.Published, "Choose a published template in this workspace.");
        foreach (var time in new[] { opensAtUtc, submissionDeadlineUtc, assessmentDeadlineUtc, appealDeadlineUtc, finalizationDeadlineUtc }) Rule.Utc(time);
        Rule.Require(opensAtUtc < submissionDeadlineUtc && submissionDeadlineUtc < assessmentDeadlineUtc &&
            assessmentDeadlineUtc < appealDeadlineUtc && appealDeadlineUtc < finalizationDeadlineUtc, "Period dates must be in strictly increasing order.");
        Name = Rule.Text(name, "Period name", 120);
        TemplateId = template.Id;
        TemplateName = template.Name;
        TemplateVersion = template.Version;
        OpensAtUtc = opensAtUtc;
        SubmissionDeadlineUtc = submissionDeadlineUtc;
        AssessmentDeadlineUtc = assessmentDeadlineUtc;
        AppealDeadlineUtc = appealDeadlineUtc;
        FinalizationDeadlineUtc = finalizationDeadlineUtc;
        Record(actorId, "Period created", now, "Draft period created from a published template.");
    }

    public void Open(AssessmentTemplate template, string actorId, DateTime now)
    {
        RequireWindow(PeriodPhase.Draft, SubmissionDeadlineUtc, now);
        Rule.Require(template.Id == TemplateId && template.WorkspaceId == WorkspaceId && template.State == TemplateState.Published,
            "The selected published template is required.");
        Rule.Require(_criteria.Count == 0 && _bands.Count == 0, "Period snapshots already exist.");
        foreach (var criterion in template.Criteria)
            _criteria.Add(new PeriodCriterion(WorkspaceId, Id, criterion, template.Categories.Single(category => category.Id == criterion.CategoryId)));
        foreach (var band in template.Bands) _bands.Add(new PeriodRatingBand(WorkspaceId, Id, band));
        Phase = PeriodPhase.SubmissionOpen;
        Record(actorId, "Period opened", now, "Criteria and rating bands snapshotted.");
    }

    public void Advance(IReadOnlyCollection<BranchAssessment> assessments, string actorId, DateTime now)
    {
        RequireAssignments(assessments);
        Rule.Utc(now);
        switch (Phase)
        {
            case PeriodPhase.SubmissionOpen:
                RequireWindow(PeriodPhase.SubmissionOpen, AssessmentDeadlineUtc, now);
                Rule.Require(assessments.All(row => row.State == AssessmentState.Submitted), "Every assigned branch must submit before assessment opens.");
                Phase = PeriodPhase.AssessmentOpen;
                break;
            case PeriodPhase.AssessmentOpen:
                RequireWindow(PeriodPhase.AssessmentOpen, AppealDeadlineUtc, now);
                Rule.Require(assessments.All(row => row.State == AssessmentState.ProvisionallyScored), "Every assessment must be completely scored before publication.");
                Phase = PeriodPhase.AppealOpen;
                break;
            case PeriodPhase.AppealOpen:
                RequireWindow(PeriodPhase.AppealOpen, FinalizationDeadlineUtc, now);
                foreach (var assessment in assessments) assessment.CloseAppealWindow(this, actorId, now);
                Phase = PeriodPhase.FinalReview;
                break;
            default:
                throw new DomainRuleException("This phase cannot advance. Opening and finalization use their dedicated actions.");
        }
        Record(actorId, "Period phase advanced", now, $"Phase: {Phase}.");
    }

    public void FinalizeResults(IReadOnlyCollection<BranchAssessment> assessments, string actorId, DateTime now)
    {
        RequireAssignments(assessments);
        RequireWindow(PeriodPhase.FinalReview, FinalizationDeadlineUtc, now);
        Rule.Require(assessments.All(row => row.State == AssessmentState.AwaitingFinalization && row.Appeals.All(appeal => appeal.Decision != AppealDecision.Pending)),
            "All assessments must be scored and all appeals decided before finalization.");
        foreach (var assessment in assessments) assessment.FinalizeResult(this, actorId, now);
        Phase = PeriodPhase.Finalized;
        Record(actorId, "Period finalized", now, "All branch results are final and read-only.");
    }

    public void RequireSubmission(DateTime now) => RequireWindow(PeriodPhase.SubmissionOpen, SubmissionDeadlineUtc, now);
    public void RequireScoring(DateTime now) => RequireWindow(PeriodPhase.AssessmentOpen, AssessmentDeadlineUtc, now);
    public void RequireAppeal(DateTime now) => RequireWindow(PeriodPhase.AppealOpen, AppealDeadlineUtc, now);
    public void RequireDecision(DateTime now)
    {
        Rule.Require(Phase is PeriodPhase.AppealOpen or PeriodPhase.FinalReview, "Appeal decisions are not open.");
        RequireWindow(Phase, FinalizationDeadlineUtc, now);
    }

    public void RequireAssignment(DateTime now)
    {
        Rule.Require(Phase is PeriodPhase.Draft or PeriodPhase.SubmissionOpen, "Assignments are closed for this period.");
        Rule.Utc(now);
        Rule.Require(now < SubmissionDeadlineUtc, "The submission deadline has passed.");
    }

    private void RequireWindow(PeriodPhase expected, DateTime deadline, DateTime now)
    {
        Rule.Utc(now);
        Rule.Require(Phase == expected, "This action is not available in the current period phase.");
        Rule.Require(now >= OpensAtUtc && now < deadline, "This action is outside the period's allowed dates.");
    }

    private void RequireAssignments(IReadOnlyCollection<BranchAssessment> assessments)
    {
        Rule.Require(assessments.Count > 0, "Assign at least one branch to the period.");
        Rule.Require(assessments.All(row => row.PeriodId == Id && row.WorkspaceId == WorkspaceId), "Assessments must belong to this period and workspace.");
    }
}
