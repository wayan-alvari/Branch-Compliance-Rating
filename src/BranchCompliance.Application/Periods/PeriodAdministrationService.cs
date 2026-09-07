using BranchCompliance.Application.Security;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Branches;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Domain.Rules;
using BranchCompliance.Domain.Templates;

namespace BranchCompliance.Application.Periods;

public sealed record AssessorOption(string Id, string Email);

public sealed record PeriodAdministrationDetails(
    AssessmentPeriod Period,
    IReadOnlyList<BranchAssessment> Assessments,
    IReadOnlyList<Branch> AvailableBranches,
    IReadOnlyList<AssessorOption> Assessors);

public interface IPeriodAdministrationStore
{
    Task<IReadOnlyList<AssessmentPeriod>> PeriodsAsync(CancellationToken cancellationToken);
    Task<AssessmentPeriod?> PeriodAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssessmentTemplate>> PublishedTemplatesAsync(CancellationToken cancellationToken);
    Task<AssessmentTemplate?> TemplateAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Branch>> ActiveBranchesAsync(CancellationToken cancellationToken);
    Task<Branch?> BranchAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssessorOption>> AssessorsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<BranchAssessment>> AssessmentsAsync(Guid periodId, CancellationToken cancellationToken);
    void Add(AssessmentPeriod period);
    void Add(BranchAssessment assessment);
    Task SaveAsync(CancellationToken cancellationToken);
}

public sealed class PeriodAdministrationService(
    IPeriodAdministrationStore store,
    IWorkspaceContext workspace,
    ICurrentActor actors,
    IClock clock)
{
    private Actor Administrator()
    {
        var actor = actors.Get();
        AccessRules.RequireRole(actor, DemoRoles.Administrator);
        if (workspace.WorkspaceId == Guid.Empty) throw new AccessDeniedException();
        return actor;
    }

    public Task<IReadOnlyList<AssessmentPeriod>> PeriodsAsync(CancellationToken cancellationToken)
    {
        Administrator();
        return store.PeriodsAsync(cancellationToken);
    }

    public Task<IReadOnlyList<AssessmentTemplate>> PublishedTemplatesAsync(CancellationToken cancellationToken)
    {
        Administrator();
        return store.PublishedTemplatesAsync(cancellationToken);
    }

    public async Task<PeriodAdministrationDetails> DetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        Administrator();
        var period = await RequirePeriodAsync(id, cancellationToken);
        var assessments = await store.AssessmentsAsync(id, cancellationToken);
        var assignedBranches = assessments.Select(row => row.BranchId).ToHashSet();
        var available = (await store.ActiveBranchesAsync(cancellationToken))
            .Where(row => !assignedBranches.Contains(row.Id)).ToArray();
        return new PeriodAdministrationDetails(period, assessments, available,
            await store.AssessorsAsync(cancellationToken));
    }

    public async Task<Guid> CreateAsync(string name, Guid templateId, DateTime opensAtUtc,
        DateTime submissionDeadlineUtc, DateTime assessmentDeadlineUtc, DateTime appealDeadlineUtc,
        DateTime finalizationDeadlineUtc, CancellationToken cancellationToken)
    {
        var actor = Administrator();
        var template = await store.TemplateAsync(templateId, cancellationToken)
            ?? throw new ResourceNotFoundException();
        var period = new AssessmentPeriod(workspace.WorkspaceId, name, template, AsUtc(opensAtUtc),
            AsUtc(submissionDeadlineUtc), AsUtc(assessmentDeadlineUtc), AsUtc(appealDeadlineUtc),
            AsUtc(finalizationDeadlineUtc), actor.Id, clock.UtcNow);
        store.Add(period);
        await store.SaveAsync(cancellationToken);
        return period.Id;
    }

    public async Task AssignAsync(Guid periodId, Guid branchId, string assessorId, CancellationToken cancellationToken)
    {
        var actor = Administrator();
        var period = await RequirePeriodAsync(periodId, cancellationToken);
        var assessments = await store.AssessmentsAsync(periodId, cancellationToken);
        Rule.Require(!assessments.Any(row => row.BranchId == branchId), "This branch is already assigned to the period.");
        var branch = await store.BranchAsync(branchId, cancellationToken) ?? throw new ResourceNotFoundException();
        Rule.Require((await store.AssessorsAsync(cancellationToken)).Any(row => row.Id == assessorId),
            "Choose an Assessor account.");
        store.Add(new BranchAssessment(period, branch, assessorId, actor.Id, clock.UtcNow));
        await store.SaveAsync(cancellationToken);
    }

    public async Task OpenAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = Administrator();
        var period = await RequirePeriodAsync(id, cancellationToken);
        var template = await store.TemplateAsync(period.TemplateId, cancellationToken)
            ?? throw new ResourceNotFoundException();
        var assessments = await store.AssessmentsAsync(id, cancellationToken);
        period.Open(template, assessments, actor.Id, clock.UtcNow);
        await store.SaveAsync(cancellationToken);
    }

    public async Task AdvanceAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = Administrator();
        var period = await RequirePeriodAsync(id, cancellationToken);
        period.Advance(await store.AssessmentsAsync(id, cancellationToken), actor.Id, clock.UtcNow);
        await store.SaveAsync(cancellationToken);
    }

    private async Task<AssessmentPeriod> RequirePeriodAsync(Guid id, CancellationToken cancellationToken)
        => await store.PeriodAsync(id, cancellationToken) ?? throw new ResourceNotFoundException();

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
