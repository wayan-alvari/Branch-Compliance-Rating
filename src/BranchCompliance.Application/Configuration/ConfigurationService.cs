using BranchCompliance.Application.Security;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Branches;
using BranchCompliance.Domain.Rules;
using BranchCompliance.Domain.Scoring;
using BranchCompliance.Domain.Templates;

namespace BranchCompliance.Application.Configuration;

public interface IConfigurationStore
{
    Task<IReadOnlyList<Branch>> BranchesAsync(CancellationToken cancellationToken);
    Task<Branch?> BranchAsync(Guid id, CancellationToken cancellationToken);
    Task<string> DemoBranchUserIdAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AssessmentTemplate>> TemplatesAsync(CancellationToken cancellationToken);
    Task<AssessmentTemplate?> TemplateAsync(Guid id, CancellationToken cancellationToken);
    Task<int> NextVersionAsync(Guid familyId, CancellationToken cancellationToken);
    void Add(Branch branch);
    void Add(AssessmentTemplate template);
    Task SaveAsync(CancellationToken cancellationToken);
}

public sealed class ConfigurationService(IConfigurationStore store, IWorkspaceContext workspace, ICurrentActor actors, IClock clock)
{
    private Actor Administrator()
    {
        var actor = actors.Get();
        AccessRules.RequireRole(actor, DemoRoles.Administrator);
        if (workspace.WorkspaceId == Guid.Empty) throw new AccessDeniedException();
        return actor;
    }

    public Task<IReadOnlyList<Branch>> BranchesAsync(CancellationToken cancellationToken)
    {
        Administrator();
        return store.BranchesAsync(cancellationToken);
    }

    public async Task<Branch> BranchAsync(Guid id, CancellationToken cancellationToken)
    {
        Administrator();
        return await store.BranchAsync(id, cancellationToken) ?? throw new ResourceNotFoundException();
    }

    public async Task<Guid> CreateBranchAsync(string code, string name, string region, bool assignDemoUser, CancellationToken cancellationToken)
    {
        var actor = Administrator();
        var normalized = code.Trim().ToUpperInvariant();
        Rule.Require(!(await store.BranchesAsync(cancellationToken)).Any(row => row.Code == normalized), "This branch code already exists.");
        var userId = assignDemoUser ? await store.DemoBranchUserIdAsync(cancellationToken) : null;
        var branch = new Branch(workspace.WorkspaceId, code, name, region, userId, actor.Id, clock.UtcNow);
        store.Add(branch);
        await store.SaveAsync(cancellationToken);
        return branch.Id;
    }

    public async Task EditBranchAsync(Guid id, string name, string region, bool active, bool assignDemoUser, CancellationToken cancellationToken)
    {
        var actor = Administrator();
        var branch = await BranchAsync(id, cancellationToken);
        branch.Edit(name, region, active, actor.Id, clock.UtcNow);
        var userId = assignDemoUser ? await store.DemoBranchUserIdAsync(cancellationToken) : null;
        if (branch.BranchUserId != userId) branch.AssignUser(userId, actor.Id, clock.UtcNow);
        await store.SaveAsync(cancellationToken);
    }

    public Task<IReadOnlyList<AssessmentTemplate>> TemplatesAsync(CancellationToken cancellationToken)
    {
        Administrator();
        return store.TemplatesAsync(cancellationToken);
    }

    public async Task<AssessmentTemplate> TemplateAsync(Guid id, CancellationToken cancellationToken)
    {
        Administrator();
        return await store.TemplateAsync(id, cancellationToken) ?? throw new ResourceNotFoundException();
    }

    public async Task<Guid> CreateTemplateAsync(string name, string description, CancellationToken cancellationToken)
    {
        var actor = Administrator();
        var template = new AssessmentTemplate(workspace.WorkspaceId, name, description, actor.Id, clock.UtcNow);
        template.SetBands([new RatingThreshold("Excellent", 90m), new("Good", 80m), new("Satisfactory", 70m), new("Needs Improvement", 0m)], actor.Id, clock.UtcNow);
        store.Add(template);
        await store.SaveAsync(cancellationToken);
        return template.Id;
    }

    public async Task RenameTemplateAsync(Guid id, string name, string description, CancellationToken cancellationToken)
    {
        var actor = Administrator();
        (await TemplateAsync(id, cancellationToken)).Rename(name, description, actor.Id, clock.UtcNow);
        await store.SaveAsync(cancellationToken);
    }

    public async Task<Guid> NewVersionAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = Administrator();
        var source = await TemplateAsync(id, cancellationToken);
        var draft = source.NewVersion(await store.NextVersionAsync(source.FamilyId, cancellationToken), actor.Id, clock.UtcNow);
        store.Add(draft);
        await store.SaveAsync(cancellationToken);
        return draft.Id;
    }

    public async Task SaveCategoryAsync(Guid templateId, Guid? id, string name, int order, CancellationToken cancellationToken)
    {
        var actor = Administrator();
        var template = await TemplateAsync(templateId, cancellationToken);
        if (id is null) template.AddCategory(name, order, actor.Id, clock.UtcNow);
        else template.UpdateCategory(id.Value, name, order, actor.Id, clock.UtcNow);
        await store.SaveAsync(cancellationToken);
    }

    public async Task RemoveCategoryAsync(Guid templateId, Guid id, CancellationToken cancellationToken)
    {
        var actor = Administrator();
        (await TemplateAsync(templateId, cancellationToken)).RemoveCategory(id, actor.Id, clock.UtcNow);
        await store.SaveAsync(cancellationToken);
    }

    public async Task SaveCriterionAsync(Guid templateId, Guid? id, Guid categoryId, string code, string title, string guidance,
        decimal weight, bool evidenceRequired, int order, CancellationToken cancellationToken)
    {
        var actor = Administrator();
        var template = await TemplateAsync(templateId, cancellationToken);
        if (id is null) template.AddCriterion(categoryId, code, title, guidance, weight, evidenceRequired, order, actor.Id, clock.UtcNow);
        else template.EditCriterion(id.Value, categoryId, code, title, guidance, weight, evidenceRequired, order, actor.Id, clock.UtcNow);
        await store.SaveAsync(cancellationToken);
    }

    public async Task RemoveCriterionAsync(Guid templateId, Guid id, CancellationToken cancellationToken)
    {
        var actor = Administrator();
        (await TemplateAsync(templateId, cancellationToken)).RemoveCriterion(id, actor.Id, clock.UtcNow);
        await store.SaveAsync(cancellationToken);
    }

    public async Task SetBandsAsync(Guid templateId, IReadOnlyList<RatingThreshold> bands, CancellationToken cancellationToken)
    {
        var actor = Administrator();
        Rule.Require(bands.Count is > 0 and <= 10, "Use between one and ten rating bands.");
        (await TemplateAsync(templateId, cancellationToken)).SetBands(bands, actor.Id, clock.UtcNow);
        await store.SaveAsync(cancellationToken);
    }

    public async Task PublishAsync(Guid id, CancellationToken cancellationToken)
    {
        var actor = Administrator();
        (await TemplateAsync(id, cancellationToken)).Publish(actor.Id, clock.UtcNow);
        await store.SaveAsync(cancellationToken);
    }
}
