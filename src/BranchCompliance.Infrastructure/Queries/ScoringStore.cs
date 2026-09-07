using BranchCompliance.Application.Scoring;
using BranchCompliance.Application.Security;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BranchCompliance.Infrastructure.Queries;

public sealed class ScoringStore(ComplianceDbContext db) : IScoringStore
{
    private IQueryable<BranchAssessment> Assessments => db.Assessments
        .Include(row => row.Responses).Include(row => row.Evidence).Include(row => row.Scores).Include(row => row.Appeals)
        .AsSplitQuery();

    private IQueryable<AssessmentPeriod> Periods => db.Periods
        .Include(row => row.Criteria).Include(row => row.Bands).AsSplitQuery();

    public async Task<IReadOnlyList<BranchAssessment>> AssessmentsAsync(string assessorId,
        CancellationToken cancellationToken)
        => await Assessments.Where(row => row.AssessorId == assessorId)
            .OrderBy(row => row.BranchName).ToListAsync(cancellationToken);

    public Task<BranchAssessment?> AssessmentAsync(Guid id, CancellationToken cancellationToken)
        => Assessments.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AssessmentPeriod>> PeriodsAsync(IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
        => await Periods.Where(row => ids.Contains(row.Id)).ToListAsync(cancellationToken);

    public Task<AssessmentPeriod?> PeriodAsync(Guid id, CancellationToken cancellationToken)
        => Periods.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException(); }
        catch (DbUpdateException) { throw new ConflictException(); }
    }
}
