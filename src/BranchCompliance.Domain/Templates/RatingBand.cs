using BranchCompliance.Domain.Workspaces;

namespace BranchCompliance.Domain.Templates;

public sealed class RatingBand : WorkspaceEntity
{
    public Guid TemplateId { get; private set; }
    public string Label { get; private set; } = "";
    public decimal MinimumInclusive { get; private set; }
    public string Color { get; private set; } = "slate";
    public int Order { get; private set; }
    private RatingBand() { }
    internal RatingBand(Guid workspaceId, Guid templateId, string label, decimal minimum, int order) : base(workspaceId)
    {
        TemplateId = templateId;
        Label = label;
        MinimumInclusive = minimum;
        Order = order;
        Color = minimum >= 90m ? "teal" : minimum >= 80m ? "blue" : minimum >= 70m ? "amber" : "slate";
    }
}
