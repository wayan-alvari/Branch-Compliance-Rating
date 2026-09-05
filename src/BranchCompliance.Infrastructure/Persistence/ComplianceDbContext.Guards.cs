using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Domain.Templates;
using BranchCompliance.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace BranchCompliance.Infrastructure.Persistence;

public partial class ComplianceDbContext
{
    private void ValidateImmutability(EntityEntry<WorkspaceEntity> entry)
    {
        if (entry.State != EntityState.Added && entry.Entity is PeriodCriterion or PeriodRatingBand or CriterionScore)
            throw new InvalidOperationException("Snapshots and score revisions are immutable.");
        if (entry.Entity is AssessmentTemplate && entry.State != EntityState.Added &&
            entry.OriginalValues.GetValue<TemplateState>(nameof(AssessmentTemplate.State)) == TemplateState.Published)
            throw new InvalidOperationException("Published template versions are immutable.");

        Guid? templateId = entry.Entity switch
        {
            TemplateCategory category => category.TemplateId,
            TemplateCriterion criterion => criterion.TemplateId,
            RatingBand band => band.TemplateId,
            _ => null
        };
        if (templateId is not null)
        {
            var template = ChangeTracker.Entries<AssessmentTemplate>().SingleOrDefault(row => row.Entity.Id == templateId);
            if (template is null || template.State != EntityState.Added && template.OriginalValues.GetValue<TemplateState>(nameof(AssessmentTemplate.State)) != TemplateState.Draft)
                throw new InvalidOperationException("Template children may change only through their draft template.");
        }

        if (entry.Entity is BranchAssessment && entry.State != EntityState.Added)
        {
            if (entry.OriginalValues.GetValue<AssessmentState>(nameof(BranchAssessment.State)) == AssessmentState.Finalized)
                throw new InvalidOperationException("Final assessments are immutable.");
            if (entry.Property(nameof(BranchAssessment.ProvisionalScore)).IsModified && entry.OriginalValues[nameof(BranchAssessment.ProvisionalScore)] is not null)
                throw new InvalidOperationException("The published provisional value is immutable.");
            foreach (var field in new[] { nameof(BranchAssessment.PeriodId), nameof(BranchAssessment.BranchId), nameof(BranchAssessment.BranchName), nameof(BranchAssessment.BranchCode), nameof(BranchAssessment.Region) })
                if (entry.Property(field).IsModified) throw new InvalidOperationException("Assessment assignment snapshots are immutable.");
        }

        Guid? assessmentId = entry.Entity switch
        {
            BranchResponse response => response.AssessmentId,
            CriterionScore score => score.AssessmentId,
            Appeal appeal => appeal.AssessmentId,
            EvidenceFile evidence => evidence.AssessmentId,
            _ => null
        };
        if (assessmentId is not null)
        {
            var assessment = ChangeTracker.Entries<BranchAssessment>().SingleOrDefault(row => row.Entity.Id == assessmentId);
            if (assessment is null) throw new InvalidOperationException("Assessment children must change through their owning assessment.");
            if (assessment.State != EntityState.Added)
            {
                var originalState = assessment.OriginalValues.GetValue<AssessmentState>(nameof(BranchAssessment.State));
                if (originalState == AssessmentState.Finalized) throw new InvalidOperationException("Final assessment records are immutable.");
                if (entry.Entity is BranchResponse && originalState is not (AssessmentState.NotStarted or AssessmentState.InProgress))
                    throw new InvalidOperationException("Submitted responses are immutable.");
                if (entry.Entity is Appeal && entry.State != EntityState.Added)
                {
                    if (entry.State == EntityState.Deleted || entry.OriginalValues.GetValue<AppealDecision>(nameof(Appeal.Decision)) != AppealDecision.Pending)
                        throw new InvalidOperationException("Decided appeals are immutable.");
                    string[] decisionFields = [nameof(Appeal.Decision), nameof(Appeal.DecisionNote), nameof(Appeal.RevisedScore), nameof(Appeal.DecidedBy), nameof(Appeal.DecidedAtUtc)];
                    if (entry.Properties.Any(property => property.IsModified && !decisionFields.Contains(property.Metadata.Name)))
                        throw new InvalidOperationException("The original appeal is immutable; only its decision may be recorded.");
                }
            }
        }
    }
}
