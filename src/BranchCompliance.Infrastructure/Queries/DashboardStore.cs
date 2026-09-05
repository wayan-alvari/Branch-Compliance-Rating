using BranchCompliance.Application.Dashboard;
using BranchCompliance.Application.Security;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BranchCompliance.Infrastructure.Queries;

public sealed class DashboardStore(ComplianceDbContext db) : IDashboardStore
{
    public async Task<DashboardModel> ReadAsync(Guid workspaceId, Actor actor, CancellationToken cancellationToken)
    {
        if (workspaceId == Guid.Empty || db.CurrentWorkspaceId != workspaceId) throw new AccessDeniedException();
        AccessRules.RequireRole(actor, DemoRoles.All);
        var allowed = db.Assessments.AsNoTracking();
        if (actor.Role == DemoRoles.BranchUser) allowed = allowed.Where(row => row.BranchUserId == actor.Id);
        if (actor.Role == DemoRoles.Assessor) allowed = allowed.Where(row => row.AssessorId == actor.Id);
        var allowedIds = await allowed.Select(row => row.Id).ToArrayAsync(cancellationToken);
        var period = await db.Periods.AsNoTracking().Where(row => row.Phase != PeriodPhase.Draft && row.Phase != PeriodPhase.Finalized)
            .Where(row => actor.Role == DemoRoles.Administrator || actor.Role == DemoRoles.Approver || allowed.Any(assessment => assessment.PeriodId == row.Id))
            .OrderByDescending(row => row.OpensAtUtc).FirstOrDefaultAsync(cancellationToken);
        PeriodSummary? summary = null;
        var queue = new List<QueueItem>();
        if (period is not null)
        {
            var criteria = await db.PeriodCriteria.AsNoTracking().Where(row => row.PeriodId == period.Id).ToListAsync(cancellationToken);
            summary = new PeriodSummary(period.Id, period.Name, period.Phase, period.SubmissionDeadlineUtc,
                period.AssessmentDeadlineUtc, period.AppealDeadlineUtc, period.FinalizationDeadlineUtc, criteria.Count);
            var active = await allowed.Where(row => row.PeriodId == period.Id)
                .Include(row => row.Responses).Include(row => row.Evidence).Include(row => row.Scores).Include(row => row.Appeals)
                .AsSplitQuery().ToListAsync(cancellationToken);
            foreach (var assessment in active.OrderBy(row => row.BranchName, StringComparer.Ordinal))
            {
                var completed = criteria.Count(criterion => assessment.Responses.Any(response => response.PeriodCriterionId == criterion.Id &&
                    response.Answer.Length > 0 && (!criterion.EvidenceRequired || assessment.Evidence.Any(file => file.ResponseId == response.Id))));
                var visible = period.Phase is PeriodPhase.AppealOpen or PeriodPhase.FinalReview or PeriodPhase.Finalized;
                queue.Add(new QueueItem(assessment.Id, assessment.BranchName, assessment.State, completed, criteria.Count,
                    assessment.Scores.Select(row => row.PeriodCriterionId).Distinct().Count(), assessment.Appeals.Count(row => row.Decision == AppealDecision.Pending),
                    visible ? assessment.ProvisionalScore : null, visible ? assessment.ProvisionalRating : null));
            }
        }
        var completedCount = await allowed.CountAsync(row => row.State == AssessmentState.Finalized, cancellationToken);
        var finished = await allowed.Where(row => row.State == AssessmentState.Finalized)
            .OrderByDescending(row => row.FinalizedAtUtc).Take(10).ToListAsync(cancellationToken);
        var periodIds = finished.Select(row => row.PeriodId).Distinct().ToArray();
        var names = await db.Periods.Where(row => periodIds.Contains(row.Id)).Select(row => new { row.Id, row.Name }).ToDictionaryAsync(row => row.Id, row => row.Name, cancellationToken);
        // Scores alone supply the global rank; unauthorized branch details never leave this store.
        var totals = await db.Assessments.Where(row => periodIds.Contains(row.PeriodId) && row.State == AssessmentState.Finalized)
            .Select(row => new { row.PeriodId, row.FinalScore }).ToListAsync(cancellationToken);
        var history = finished.Select(row => new ResultTeaser(row.Id, row.BranchName, names[row.PeriodId], row.FinalScore!.Value,
                row.FinalRating!, 1 + totals.Count(other => other.PeriodId == row.PeriodId && other.FinalScore > row.FinalScore)))
            .OrderBy(row => row.PeriodName, StringComparer.Ordinal).ThenBy(row => row.Rank).ThenBy(row => row.BranchName, StringComparer.Ordinal).ToArray();
        var audits = db.AuditEvents.AsNoTracking();
        if (actor.Role is DemoRoles.BranchUser or DemoRoles.Assessor) audits = audits.Where(row => allowedIds.Contains(row.EntityId));
        if (actor.Role == DemoRoles.Approver)
            audits = audits.Where(row => row.Action == "Criterion appealed" || row.Action == "Appeal accepted" || row.Action == "Appeal rejected" ||
                row.Action == "Scoring completed" || row.Action == "Branch result finalized" || row.Action == "Period finalized");
        var activity = await audits.OrderByDescending(row => row.AtUtc).ThenBy(row => row.Id).Take(8)
            .Select(row => new ActivityItem(row.AtUtc, row.Action, row.Details)).ToArrayAsync(cancellationToken);
        return new DashboardModel(actor.Role, actor.DisplayName, summary, queue, history, activity, completedCount);
    }
}
