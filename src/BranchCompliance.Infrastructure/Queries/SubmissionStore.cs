using BranchCompliance.Application.Security;
using BranchCompliance.Application.Submissions;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BranchCompliance.Infrastructure.Queries;

public sealed class SubmissionStore(ComplianceDbContext db) : ISubmissionStore
{
    private IQueryable<BranchAssessment> Assessments => db.Assessments
        .Include(row => row.Responses).Include(row => row.Evidence).Include(row => row.Scores).Include(row => row.Appeals)
        .AsSplitQuery();

    private IQueryable<AssessmentPeriod> Periods => db.Periods
        .Include(row => row.Criteria).Include(row => row.Bands).AsSplitQuery();

    public async Task<IReadOnlyList<BranchAssessment>> AssessmentsAsync(string? branchUserId,
        CancellationToken cancellationToken)
    {
        var query = Assessments;
        if (branchUserId is not null) query = query.Where(row => row.BranchUserId == branchUserId);
        return await query.OrderBy(row => row.BranchName).ToListAsync(cancellationToken);
    }

    public Task<BranchAssessment?> AssessmentAsync(Guid id, CancellationToken cancellationToken)
        => Assessments.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AssessmentPeriod>> PeriodsAsync(IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
        => await Periods.Where(row => ids.Contains(row.Id)).ToListAsync(cancellationToken);

    public Task<AssessmentPeriod?> PeriodAsync(Guid id, CancellationToken cancellationToken)
        => Periods.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);

    public Task<EvidenceFile?> EvidenceAsync(Guid id, CancellationToken cancellationToken)
        => db.EvidenceFiles.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);

    public void Remove(EvidenceFile evidence) => db.EvidenceFiles.Remove(evidence);

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException(); }
        catch (DbUpdateException) { throw new ConflictException(); }
    }
}
