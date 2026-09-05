using BranchCompliance.Domain.Templates;
using BranchCompliance.Domain.Workspaces;

namespace BranchCompliance.Domain.Periods;

public sealed class PeriodRatingBand : WorkspaceEntity
{
    public Guid PeriodId { get; private set; }
    public string Label { get; private set; } = "";
    public decimal MinimumInclusive { get; private set; }
    public string Color { get; private set; } = "";
    public int Order { get; private set; }
    private PeriodRatingBand() { }
    internal PeriodRatingBand(Guid workspaceId, Guid periodId, RatingBand source) : base(workspaceId)
    {
        PeriodId = periodId;
        Label = source.Label;
        MinimumInclusive = source.MinimumInclusive;
        Color = source.Color;
        Order = source.Order;
    }
}
