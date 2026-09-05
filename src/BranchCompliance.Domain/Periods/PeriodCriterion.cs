using BranchCompliance.Domain.Templates;
using BranchCompliance.Domain.Workspaces;

namespace BranchCompliance.Domain.Periods;

public sealed class PeriodCriterion : WorkspaceEntity
{
    public Guid PeriodId { get; private set; }
    public Guid SourceCriterionId { get; private set; }
    public string Category { get; private set; } = "";
    public int CategoryOrder { get; private set; }
    public string Code { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string Guidance { get; private set; } = "";
    public decimal Weight { get; private set; }
    public bool EvidenceRequired { get; private set; }
    public int Order { get; private set; }
    private PeriodCriterion() { }

    internal PeriodCriterion(Guid workspaceId, Guid periodId, TemplateCriterion criterion, TemplateCategory category) : base(workspaceId)
    {
        PeriodId = periodId;
        SourceCriterionId = criterion.Id;
        Category = category.Name;
        CategoryOrder = category.Order;
        Code = criterion.Code;
        Title = criterion.Title;
        Guidance = criterion.Guidance;
        Weight = criterion.Weight;
        EvidenceRequired = criterion.EvidenceRequired;
        Order = criterion.Order;
    }
}
