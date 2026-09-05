using BranchCompliance.Domain.Rules;
using BranchCompliance.Domain.Scoring;
using BranchCompliance.Domain.Workspaces;

namespace BranchCompliance.Domain.Templates;

public enum TemplateState { Draft, Published }

public sealed class AssessmentTemplate : WorkspaceEntity
{
    private readonly List<TemplateCategory> _categories = [];
    private readonly List<TemplateCriterion> _criteria = [];
    private readonly List<RatingBand> _bands = [];
    public Guid FamilyId { get; private set; }
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public int Version { get; private set; }
    public TemplateState State { get; private set; }
    public IReadOnlyCollection<TemplateCategory> Categories => _categories.AsReadOnly();
    public IReadOnlyCollection<TemplateCriterion> Criteria => _criteria.AsReadOnly();
    public IReadOnlyCollection<RatingBand> Bands => _bands.AsReadOnly();
    private AssessmentTemplate() { }

    public AssessmentTemplate(Guid workspaceId, string name, string description, string actorId, DateTime now)
        : base(workspaceId)
    {
        FamilyId = Guid.NewGuid();
        Version = 1;
        Rename(name, description, actorId, now);
    }

    public void Rename(string name, string description, string actorId, DateTime now)
    {
        RequireDraft();
        Name = Rule.Text(name, "Template name", 120);
        Description = Rule.Text(description, "Description", 1000, required: false);
        Record(actorId, "Template draft updated", now, $"Version {Version}.");
    }

    public TemplateCategory AddCategory(string name, int order, string actorId, DateTime now)
    {
        RequireDraft();
        name = Rule.Text(name, "Category name", 100);
        Rule.Require(!_categories.Any(row => row.Name.Equals(name, StringComparison.OrdinalIgnoreCase)), "Category names must be unique within a template.");
        Rule.Require(order >= 0 && order <= 999, "Display order must be between 0 and 999.");
        var category = new TemplateCategory(WorkspaceId, Id, name, order);
        _categories.Add(category);
        Record(actorId, "Template category added", now, "Draft category added.");
        return category;
    }

    public TemplateCriterion AddCriterion(Guid categoryId, string code, string title, string guidance, decimal weight,
        bool evidenceRequired, int order, string actorId, DateTime now)
    {
        RequireDraft();
        Rule.Require(_categories.Any(row => row.Id == categoryId), "Choose a category from this template.");
        code = Rule.Text(code, "Criterion code", 24).ToUpperInvariant();
        Rule.Require(code.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'), "Criterion codes use English letters, digits, and hyphens only.");
        Rule.Require(!_criteria.Any(row => row.Code == code), "Criterion codes must be unique within a template.");
        var criterion = new TemplateCriterion(WorkspaceId, Id, categoryId, code, title, guidance, weight, evidenceRequired, order);
        _criteria.Add(criterion);
        Record(actorId, "Template criterion added", now, $"Criterion {code}.");
        return criterion;
    }

    public void UpdateCriterion(Guid criterionId, string title, string guidance, decimal weight, bool evidenceRequired,
        int order, string actorId, DateTime now)
    {
        RequireDraft();
        var criterion = _criteria.SingleOrDefault(row => row.Id == criterionId);
        Rule.Require(criterion is not null, "Criterion not found in this template.");
        criterion!.Update(title, guidance, weight, evidenceRequired, order);
        Record(actorId, "Template criterion updated", now, $"Criterion {criterion.Code}.");
    }

    public void RemoveCriterion(Guid criterionId, string actorId, DateTime now)
    {
        RequireDraft();
        Rule.Require(_criteria.RemoveAll(row => row.Id == criterionId) == 1, "Criterion not found in this template.");
        Record(actorId, "Draft criterion removed", now, "A draft criterion was removed.");
    }

    public void SetBands(IEnumerable<RatingThreshold> bands, string actorId, DateTime now)
    {
        RequireDraft();
        var rows = bands.ToArray();
        WeightedScoring.ValidateBands(rows);
        _bands.Clear();
        var order = 0;
        foreach (var band in rows.OrderByDescending(row => row.MinimumInclusive))
            _bands.Add(new RatingBand(WorkspaceId, Id, band.Label, band.MinimumInclusive, order++));
        Record(actorId, "Template rating bands updated", now, "Draft rating thresholds updated.");
    }

    public void Publish(string actorId, DateTime now)
    {
        RequireDraft();
        Rule.Require(_categories.Count > 0 && _criteria.Count > 0, "Publishing requires at least one category and criterion.");
        Rule.Require(_criteria.Sum(row => row.Weight) == 100.00m, "Criterion weights must total exactly 100.00.");
        Rule.Require(_criteria.Select(row => row.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() == _criteria.Count,
            "Criterion codes must be unique.");
        WeightedScoring.ValidateBands(_bands.Select(row => new RatingThreshold(row.Label, row.MinimumInclusive)));
        State = TemplateState.Published;
        Record(actorId, "Template published", now, $"Version {Version}; weights total 100.00.");
    }

    public AssessmentTemplate NewVersion(int nextVersion, string actorId, DateTime now)
    {
        Rule.Require(State == TemplateState.Published, "Create a new version from a published template.");
        Rule.Require(nextVersion > Version, "The new version must be greater than the source version.");
        var draft = new AssessmentTemplate(WorkspaceId, Name, Description, actorId, now) { FamilyId = FamilyId, Version = nextVersion };
        var categoryIds = new Dictionary<Guid, Guid>();
        foreach (var category in _categories)
            categoryIds[category.Id] = draft.AddCategory(category.Name, category.Order, actorId, now).Id;
        foreach (var criterion in _criteria)
            draft.AddCriterion(categoryIds[criterion.CategoryId], criterion.Code, criterion.Title, criterion.Guidance,
                criterion.Weight, criterion.EvidenceRequired, criterion.Order, actorId, now);
        draft.SetBands(_bands.Select(row => new RatingThreshold(row.Label, row.MinimumInclusive)), actorId, now);
        return draft;
    }

    private void RequireDraft() => Rule.Require(State == TemplateState.Draft, "Published templates are immutable. Create a new draft version.");
}
