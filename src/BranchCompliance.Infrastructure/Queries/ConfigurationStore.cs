using BranchCompliance.Application.Configuration;
using BranchCompliance.Application.Security;
using BranchCompliance.Domain.Branches;
using BranchCompliance.Domain.Templates;
using BranchCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BranchCompliance.Infrastructure.Queries;

public sealed class ConfigurationStore(ComplianceDbContext db) : IConfigurationStore
{
    public async Task<IReadOnlyList<Branch>> BranchesAsync(CancellationToken cancellationToken)
        => await db.Branches.OrderBy(row => row.Name).ToListAsync(cancellationToken);
    public Task<Branch?> BranchAsync(Guid id, CancellationToken cancellationToken)
        => db.Branches.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
    public async Task<string> DemoBranchUserIdAsync(CancellationToken cancellationToken)
        => await (from user in db.Users
                  join membership in db.UserRoles on user.Id equals membership.UserId
                  join role in db.Roles on membership.RoleId equals role.Id
                  where role.Name == DemoRoles.BranchUser && user.Email == "branch@compliance.demo"
                  select user.Id).SingleAsync(cancellationToken);
    private IQueryable<AssessmentTemplate> Templates => db.Templates.Include(row => row.Categories).Include(row => row.Criteria).Include(row => row.Bands).AsSplitQuery();
    public async Task<IReadOnlyList<AssessmentTemplate>> TemplatesAsync(CancellationToken cancellationToken)
        => await Templates.OrderBy(row => row.Name).ThenByDescending(row => row.Version).ToListAsync(cancellationToken);
    public Task<AssessmentTemplate?> TemplateAsync(Guid id, CancellationToken cancellationToken)
        => Templates.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
    public async Task<int> NextVersionAsync(Guid familyId, CancellationToken cancellationToken)
        => (await db.Templates.Where(row => row.FamilyId == familyId).MaxAsync(row => (int?)row.Version, cancellationToken) ?? 0) + 1;
    public void Add(Branch branch) => db.Branches.Add(branch);
    public void Add(AssessmentTemplate template) => db.Templates.Add(template);
    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { throw new ConflictException(); }
    }
}
