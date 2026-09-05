using BranchCompliance.Domain.Workspaces;

namespace BranchCompliance.Domain.Templates;

public sealed class TemplateCategory : WorkspaceEntity
{
    public Guid TemplateId { get; private set; }
    public string Name { get; private set; } = "";
    public int Order { get; private set; }
    private TemplateCategory() { }
    internal TemplateCategory(Guid workspaceId, Guid templateId, string name, int order) : base(workspaceId)
    {
        TemplateId = templateId;
        Name = name;
        Order = order;
    }
}
