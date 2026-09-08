using BranchCompliance.Application.Reports;
using BranchCompliance.Application.Security;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Domain.Scoring;
using BranchCompliance.Domain.Workspaces;
using BranchCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BranchCompliance.Infrastructure.Queries;

public sealed class ReportStore(ComplianceDbContext db) : IReportStore
{
    public async Task<IReadOnlyList<AssessmentPeriod>> PeriodsAsync(CancellationToken cancellationToken)
        => await db.Periods.AsNoTracking().Include(row => row.Bands)
            .OrderByDescending(row => row.OpensAtUtc).ThenBy(row => row.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BranchAssessment>> AssessmentsAsync(Guid periodId, Actor actor,
        CancellationToken cancellationToken)
    {
        var query = AuthorizedAssessments(actor).Where(row => row.PeriodId == periodId);
        return await query.AsNoTracking().OrderBy(row => row.BranchName).ThenBy(row => row.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ResultRank>> RankingAsync(Guid periodId,
        CancellationToken cancellationToken)
    {
        var input = await db.Assessments.AsNoTracking()
            .Where(row => row.PeriodId == periodId && row.State == AssessmentState.Finalized && row.FinalScore != null)
            .Select(row => new RankingInput(row.Id, row.BranchName, row.FinalScore!.Value))
            .ToListAsync(cancellationToken);
        return WeightedScoring.Rank(input).Select(row => new ResultRank(row.AssessmentId, row.Rank)).ToArray();
    }

    public async Task<(AssessmentPeriod Period, BranchAssessment Assessment)?> DetailsAsync(Guid assessmentId,
        CancellationToken cancellationToken)
    {
        var assessment = await db.Assessments.Where(row => row.Id == assessmentId)
            .Include(row => row.Scores).Include(row => row.Appeals).AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);
        if (assessment is null) return null;
        var period = await db.Periods.Where(row => row.Id == assessment.PeriodId)
            .Include(row => row.Criteria).Include(row => row.Bands).AsSplitQuery()
            .SingleAsync(cancellationToken);
        return (period, assessment);
    }

    public async Task<IReadOnlyList<AuditRow>> AuditAsync(Actor actor, AuditFilter filter, int take,
        CancellationToken cancellationToken)
    {
        var query = db.AuditEvents.AsNoTracking();
        if (actor.Role is DemoRoles.BranchUser or DemoRoles.Assessor)
        {
            var assessments = AuthorizedAssessments(actor).Select(row => row.Id);
            query = query.Where(row => assessments.Contains(row.EntityId));
        }
        else if (actor.Role == DemoRoles.Approver)
        {
            query = query.Where(row => ApprovalActions.Contains(row.Action));
        }
        else if (actor.Role != DemoRoles.Administrator)
        {
            throw new AccessDeniedException();
        }

        if (filter.FromUtc is not null) query = query.Where(row => row.AtUtc >= filter.FromUtc.Value);
        if (filter.ToExclusiveUtc is not null) query = query.Where(row => row.AtUtc < filter.ToExclusiveUtc.Value);
        if (filter.Query.Length > 0)
            query = query.Where(row => row.Action.Contains(filter.Query) || row.Details.Contains(filter.Query));
        if (filter.Actor.Length > 0)
        {
            var actorIds = db.Users.Where(row => row.Email != null && row.Email.Contains(filter.Actor))
                .Select(row => row.Id);
            query = query.Where(row => actorIds.Contains(row.ActorId) || row.ActorId.Contains(filter.Actor));
        }

        var events = await query.OrderByDescending(row => row.AtUtc).ThenBy(row => row.Id)
            .Take(take).ToListAsync(cancellationToken);
        var ids = events.Select(row => row.ActorId).Distinct().ToArray();
        var emails = await db.Users.Where(row => ids.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, row => row.Email!, cancellationToken);
        return events.Select(row => new AuditRow(row.AtUtc,
            emails.TryGetValue(row.ActorId, out var email) ? email : row.ActorId,
            row.Action, row.EntityId, row.Details)).ToArray();
    }

    private IQueryable<BranchAssessment> AuthorizedAssessments(Actor actor)
    {
        AccessRules.RequireRole(actor, DemoRoles.All);
        var query = db.Assessments.AsQueryable();
        if (actor.Role == DemoRoles.BranchUser) query = query.Where(row => row.BranchUserId == actor.Id);
        if (actor.Role == DemoRoles.Assessor) query = query.Where(row => row.AssessorId == actor.Id);
        return query;
    }

    private static readonly string[] ApprovalActions =
    [
        "Scoring completed", "Criterion appealed", "Assessment awaiting finalization",
        "Appeal accepted", "Appeal rejected", "Branch result finalized", "Period finalized"
    ];
}
