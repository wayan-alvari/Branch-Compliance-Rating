using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Branches;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Domain.Rules;
using BranchCompliance.Domain.Templates;

namespace BranchCompliance.UnitTests;

public sealed class AssessmentWorkflowTests
{
    private static readonly DateTime Now = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

    private static (AssessmentTemplate Template, AssessmentPeriod Period, BranchAssessment Assessment) Scenario()
    {
        var template = TemplateAndScoringTests.Template();
        template.Publish("admin", Now);
        var period = new AssessmentPeriod(template.WorkspaceId, "Fictional review", template, Now.AddDays(-1),
            Now.AddDays(1), Now.AddDays(2), Now.AddDays(3), Now.AddDays(4), "admin", Now);
        var branch = new Branch(template.WorkspaceId, "DEMO-HP", "Harbor Point", "Coastal", "branch", "admin", Now);
        var assessment = new BranchAssessment(period, branch, "assessor", "admin", Now);
        period.Open(template, [assessment], "admin", Now);
        return (template, period, assessment);
    }

    private static void Submit(AssessmentPeriod period, BranchAssessment assessment)
    {
        foreach (var criterion in period.Criteria)
        {
            var response = assessment.SaveResponse(period, criterion.Id, "Fictional response for a portfolio exercise.", "", "branch", Now);
            if (criterion.EvidenceRequired)
                assessment.AddEvidence(period, new EvidenceFile(period.WorkspaceId, assessment.Id, response.Id, null, "synthetic.pdf",
                    "application/pdf", 32, new string('A', 64), "branch", Now), "branch", Now);
        }
        assessment.Submit(period, "branch", Now);
    }

    private static void Score(AssessmentPeriod period, BranchAssessment assessment)
    {
        period.Advance([assessment], "admin", Now);
        var criteria = period.Criteria.ToArray();
        assessment.ScoreCriterion(period, criteria[0].Id, 80m, "", "assessor", Now);
        assessment.ScoreCriterion(period, criteria[1].Id, 60m, "The fictional response leaves part of the criterion unexplained.", "assessor", Now);
        assessment.CompleteScoring(period, "assessor", Now);
    }

    [Fact]
    public void Full_workflow_keeps_original_scores_and_recomputes_only_final_result_after_decisions()
    {
        var (_, period, assessment) = Scenario();
        Assert.Equal(AssessmentState.NotStarted, assessment.State);
        Assert.Throws<DomainRuleException>(() => period.Advance([assessment], "admin", Now));
        Submit(period, assessment);
        Assert.Equal(AssessmentState.Submitted, assessment.State);
        Score(period, assessment);
        Assert.Equal(70m, assessment.ProvisionalScore);
        Assert.Equal("Satisfactory", assessment.ProvisionalRating);
        var criteria = period.Criteria.ToArray();
        Assert.Throws<DomainRuleException>(() => assessment.SubmitAppeal(period, criteria[0].Id, "Reason", "", "branch", Now));
        period.Advance([assessment], "admin", Now);
        var accepted = assessment.SubmitAppeal(period, criteria[1].Id, "Please consider this additional fictional clarification.", "A second example supports the response.", "branch", Now);
        var rejected = assessment.SubmitAppeal(period, criteria[0].Id, "Please recheck the original response.", "", "branch", Now);
        Assert.Equal(AssessmentState.AppealPending, assessment.State);
        period.Advance([assessment], "admin", Now);
        Assert.Equal(PeriodPhase.FinalReview, period.Phase);
        Assert.Throws<DomainRuleException>(() => period.FinalizeResults([assessment], "approver", Now));
        assessment.DecideAppeal(period, accepted.Id, true, "The clarification supports a revised score.", 90m, "approver", Now);
        Assert.Equal(AssessmentState.AppealPending, assessment.State);
        assessment.DecideAppeal(period, rejected.Id, false, "The original score remains supported by the response.", null, "approver", Now);
        Assert.Equal(AssessmentState.AwaitingFinalization, assessment.State);
        Assert.Equal(60m, accepted.OriginalScore);
        Assert.Contains(assessment.Scores, row => row.PeriodCriterionId == criteria[1].Id && row.Revision == 1 && row.Value == 60m);
        Assert.Equal(90m, assessment.LatestScore(criteria[1].Id)!.Value);
        Assert.Equal(70m, assessment.ProvisionalScore);
        period.FinalizeResults([assessment], "approver", Now);
        Assert.Equal(PeriodPhase.Finalized, period.Phase);
        Assert.Equal(AssessmentState.Finalized, assessment.State);
        Assert.Equal(85m, assessment.FinalScore);
        Assert.Equal("Good", assessment.FinalRating);
        Assert.Throws<DomainRuleException>(() => assessment.SaveResponse(period, criteria[0].Id, "Changed", "", "branch", Now));
        Assert.Throws<DomainRuleException>(() => assessment.ScoreCriterion(period, criteria[0].Id, 0m, "Changed", "assessor", Now));
        Assert.Throws<DomainRuleException>(() => assessment.DecideAppeal(period, accepted.Id, true, "Changed", 100m, "approver", Now));
        Assert.Contains(assessment.PendingAudit, row => row.Action == "Branch submitted");
        Assert.Contains(assessment.PendingAudit, row => row.Action == "Appeal accepted");
        Assert.Contains(assessment.PendingAudit, row => row.Action == "Appeal rejected");
        Assert.Contains(assessment.PendingAudit, row => row.Action == "Branch result finalized");
    }

    [Fact]
    public void Submission_requires_every_answer_and_required_evidence_then_freezes_responses()
    {
        var (_, period, assessment) = Scenario();
        Assert.Throws<DomainRuleException>(() => assessment.Submit(period, "branch", Now));
        foreach (var criterion in period.Criteria)
            assessment.SaveResponse(period, criterion.Id, "Fictional draft answer.", "", "branch", Now);
        Assert.Equal(AssessmentState.InProgress, assessment.State);
        Assert.Throws<DomainRuleException>(() => assessment.Submit(period, "branch", Now));
        Submit(period, assessment);
        Assert.Throws<DomainRuleException>(() => assessment.RemoveEvidence(period, assessment.Evidence.First().Id, "branch", Now));
        Assert.Throws<DomainRuleException>(() => assessment.SaveResponse(period, period.Criteria.First().Id, "Changed", "", "branch", Now));
    }

    [Fact]
    public void Scoring_requires_every_criterion_and_below_seventy_notes_and_retains_draft_revisions()
    {
        var (_, period, assessment) = Scenario();
        Submit(period, assessment);
        period.Advance([assessment], "admin", Now);
        var criterion = period.Criteria.First();
        Assert.Throws<DomainRuleException>(() => assessment.CompleteScoring(period, "assessor", Now));
        Assert.Throws<DomainRuleException>(() => assessment.ScoreCriterion(period, criterion.Id, 69.99m, "", "assessor", Now));
        assessment.ScoreCriterion(period, criterion.Id, 70m, "", "assessor", Now);
        assessment.ScoreCriterion(period, criterion.Id, 80m, "A second review supports this score.", "assessor", Now);
        Assert.Equal(2, assessment.Scores.Count);
        Assert.Equal(2, assessment.LatestScore(criterion.Id)!.Revision);
        Assert.Throws<DomainRuleException>(() => assessment.CompleteScoring(period, "assessor", Now));
        Assert.Throws<DomainRuleException>(() => period.Advance([assessment], "admin", Now));
    }

    [Fact]
    public void Appeals_are_unique_require_reason_and_decision_note_and_acceptance_requires_revised_score()
    {
        var (_, period, assessment) = Scenario();
        Submit(period, assessment);
        Score(period, assessment);
        period.Advance([assessment], "admin", Now);
        var criterion = period.Criteria.First();
        Assert.Throws<DomainRuleException>(() => assessment.SubmitAppeal(period, criterion.Id, "", "", "branch", Now));
        var appeal = assessment.SubmitAppeal(period, criterion.Id, "Fictional clarification.", "", "branch", Now);
        Assert.Throws<DomainRuleException>(() => assessment.SubmitAppeal(period, criterion.Id, "Duplicate request.", "", "branch", Now));
        Assert.Throws<DomainRuleException>(() => assessment.DecideAppeal(period, appeal.Id, true, "Accepted.", null, "approver", Now));
        Assert.Throws<DomainRuleException>(() => assessment.DecideAppeal(period, appeal.Id, false, "", null, "approver", Now));
        Assert.Throws<DomainRuleException>(() => assessment.DecideAppeal(period, appeal.Id, false, "Rejected.", 99m, "approver", Now));
        assessment.DecideAppeal(period, appeal.Id, true, "An explicit zero is a valid revised score.", 0m, "approver", Now);
        Assert.Equal(0m, assessment.LatestScore(criterion.Id)!.Value);
        Assert.Throws<DomainRuleException>(() => assessment.DecideAppeal(period, appeal.Id, false, "Second decision.", null, "approver", Now));
    }

    [Fact]
    public void Snapshot_and_rating_bands_do_not_change_when_a_new_template_version_changes()
    {
        var (template, period, assessment) = Scenario();
        var snapshot = period.Criteria.First();
        var draft = template.NewVersion(2, "admin", Now);
        draft.UpdateCriterion(draft.Criteria.First().Id, "New title", "New guidance", 40m, true, 8, "admin", Now);
        draft.SetBands([new("New generic band", 0m)], "admin", Now);
        Assert.Equal("Shared area readiness", snapshot.Title);
        Assert.Equal(50m, snapshot.Weight);
        Assert.False(snapshot.EvidenceRequired);
        Assert.Equal(4, period.Bands.Count);
        Assert.Equal(90m, period.Bands.Max(row => row.MinimumInclusive));
        Assert.Throws<DomainRuleException>(() => period.Open(template, [assessment], "admin", Now));
    }

    [Fact]
    public void Each_window_excludes_its_deadline_and_no_appeal_path_can_finalize()
    {
        var (_, period, assessment) = Scenario();
        period.RequireSubmission(period.SubmissionDeadlineUtc.AddTicks(-1));
        Assert.Throws<DomainRuleException>(() => period.RequireSubmission(period.SubmissionDeadlineUtc));
        Submit(period, assessment);
        Score(period, assessment);
        Assert.Throws<DomainRuleException>(() => period.RequireScoring(period.AssessmentDeadlineUtc));
        period.Advance([assessment], "admin", Now);
        period.RequireAppeal(period.AppealDeadlineUtc.AddTicks(-1));
        Assert.Throws<DomainRuleException>(() => period.RequireAppeal(period.AppealDeadlineUtc));
        period.Advance([assessment], "admin", Now);
        Assert.Equal(AssessmentState.AwaitingFinalization, assessment.State);
        Assert.Throws<DomainRuleException>(() => period.FinalizeResults([assessment], "approver", period.FinalizationDeadlineUtc));
        period.FinalizeResults([assessment], "approver", Now);
        Assert.Equal(70m, assessment.FinalScore);
    }

    [Fact]
    public void Different_workspace_periods_and_inactive_branch_assignments_are_rejected()
    {
        var (_, period, assessment) = Scenario();
        var (_, another, _) = Scenario();
        Assert.Throws<DomainRuleException>(() => assessment.SaveResponse(another, another.Criteria.First().Id, "Wrong workspace", "", "branch", Now));
        Assert.Throws<DomainRuleException>(() => period.Advance([], "admin", Now));
        var branch = new Branch(period.WorkspaceId, "DEMO-NF", "Northfield", "Inland", null, "admin", Now);
        branch.Edit(branch.Name, branch.Region, false, "admin", Now);
        Assert.Throws<DomainRuleException>(() => new BranchAssessment(period, branch, "assessor", "admin", Now));
    }

    [Fact]
    public void Draft_period_requires_an_assignment_before_it_can_snapshot_and_open()
    {
        var template = TemplateAndScoringTests.Template();
        template.Publish("admin", Now);
        var period = new AssessmentPeriod(template.WorkspaceId, "Unassigned practice", template, Now.AddHours(-1),
            Now.AddDays(1), Now.AddDays(2), Now.AddDays(3), Now.AddDays(4), "admin", Now);

        Assert.Throws<DomainRuleException>(() => period.Open(template, [], "admin", Now));
        Assert.Empty(period.Criteria);
        Assert.Equal(PeriodPhase.Draft, period.Phase);
    }
}
