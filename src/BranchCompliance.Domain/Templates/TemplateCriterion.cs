using BranchCompliance.Domain.Rules;
using BranchCompliance.Domain.Workspaces;

namespace BranchCompliance.Domain.Templates;

public sealed class TemplateCriterion : WorkspaceEntity
{
    public Guid TemplateId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Code { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string Guidance { get; private set; } = "";
    public decimal Weight { get; private set; }
    public bool EvidenceRequired { get; private set; }
    public int Order { get; private set; }
    private TemplateCriterion() { }
    internal TemplateCriterion(Guid workspaceId, Guid templateId, Guid categoryId, string code, string title,
        string guidance, decimal weight, bool evidenceRequired, int order) : base(workspaceId)
    {
        TemplateId = templateId;
        CategoryId = categoryId;
        Code = code;
        Update(title, guidance, weight, evidenceRequired, order);
    }
    internal void Update(string title, string guidance, decimal weight, bool evidenceRequired, int order)
    {
        Title = Rule.Text(title, "Criterion title", 160);
        Guidance = Rule.Text(guidance, "Guidance", 2000);
        Weight = Rule.Percentage(weight, "Weight", positive: true);
        Rule.Require(order >= 0 && order <= 999, "Display order must be between 0 and 999.");
        EvidenceRequired = evidenceRequired;
        Order = order;
    }
}
