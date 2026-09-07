using BranchCompliance.Application.Approval;
using BranchCompliance.Application.Security;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BranchCompliance.Infrastructure.Queries;

public sealed class ApprovalStore(ComplianceDbContext db) : IApprovalStore
{
    private IQueryable<AssessmentPeriod> Periods => db.Periods
        .Include(row => row.Criteria).Include(row => row.Bands).AsSplitQuery();

    public async Task<IReadOnlyList<AssessmentPeriod>> PeriodsAsync(CancellationToken cancellationToken)
        => await Periods.OrderByDescending(row => row.OpensAtUtc).ToListAsync(cancellationToken);

    public Task<AssessmentPeriod?> PeriodAsync(Guid id, CancellationToken cancellationToken)
        => Periods.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);

    public async Task<IReadOnlyList<BranchAssessment>> AssessmentsAsync(Guid periodId,
        CancellationToken cancellationToken)
        => await db.Assessments.Where(row => row.PeriodId == periodId)
            .Include(row => row.Responses).Include(row => row.Evidence).Include(row => row.Scores).Include(row => row.Appeals)
            .AsSplitQuery().OrderBy(row => row.BranchName).ToListAsync(cancellationToken);

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException(); }
        catch (DbUpdateException) { throw new ConflictException(); }
    }
}
