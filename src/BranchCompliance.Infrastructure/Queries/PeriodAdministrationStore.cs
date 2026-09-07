using BranchCompliance.Application.Periods;
using BranchCompliance.Application.Security;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Branches;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Domain.Templates;
using BranchCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BranchCompliance.Infrastructure.Queries;

public sealed class PeriodAdministrationStore(ComplianceDbContext db) : IPeriodAdministrationStore
{
    private IQueryable<AssessmentPeriod> Periods => db.Periods
        .Include(row => row.Criteria).Include(row => row.Bands).AsSplitQuery();

    private IQueryable<AssessmentTemplate> Templates => db.Templates
        .Include(row => row.Categories).Include(row => row.Criteria).Include(row => row.Bands).AsSplitQuery();

    public async Task<IReadOnlyList<AssessmentPeriod>> PeriodsAsync(CancellationToken cancellationToken)
        => await Periods.OrderByDescending(row => row.OpensAtUtc).ThenBy(row => row.Name).ToListAsync(cancellationToken);

    public Task<AssessmentPeriod?> PeriodAsync(Guid id, CancellationToken cancellationToken)
        => Periods.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AssessmentTemplate>> PublishedTemplatesAsync(CancellationToken cancellationToken)
        => await Templates.Where(row => row.State == TemplateState.Published)
            .OrderBy(row => row.Name).ThenByDescending(row => row.Version).ToListAsync(cancellationToken);

    public Task<AssessmentTemplate?> TemplateAsync(Guid id, CancellationToken cancellationToken)
        => Templates.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Branch>> ActiveBranchesAsync(CancellationToken cancellationToken)
        => await db.Branches.Where(row => row.IsActive).OrderBy(row => row.Name).ToListAsync(cancellationToken);

    public Task<Branch?> BranchAsync(Guid id, CancellationToken cancellationToken)
        => db.Branches.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AssessorOption>> AssessorsAsync(CancellationToken cancellationToken)
        => await (from user in db.Users
                  join membership in db.UserRoles on user.Id equals membership.UserId
                  join role in db.Roles on membership.RoleId equals role.Id
                  where role.Name == DemoRoles.Assessor
                  orderby user.Email
                  select new AssessorOption(user.Id, user.Email!)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BranchAssessment>> AssessmentsAsync(Guid periodId, CancellationToken cancellationToken)
        => await db.Assessments.Where(row => row.PeriodId == periodId)
            .Include(row => row.Responses).Include(row => row.Evidence).Include(row => row.Scores).Include(row => row.Appeals)
            .AsSplitQuery().OrderBy(row => row.BranchName).ToListAsync(cancellationToken);

    public void Add(AssessmentPeriod period) => db.Periods.Add(period);
    public void Add(BranchAssessment assessment) => db.Assessments.Add(assessment);

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException(); }
        catch (DbUpdateException) { throw new ConflictException(); }
    }
}
