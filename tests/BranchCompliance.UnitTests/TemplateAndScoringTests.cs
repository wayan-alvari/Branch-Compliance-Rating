using BranchCompliance.Domain.Rules;
using BranchCompliance.Domain.Scoring;
using BranchCompliance.Domain.Templates;

namespace BranchCompliance.UnitTests;

public sealed class TemplateAndScoringTests
{
    private static readonly DateTime Now = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);
    public static readonly RatingThreshold[] Bands = [new("Excellent", 90m), new("Good", 80m), new("Satisfactory", 70m), new("Needs Improvement", 0m)];

    public static AssessmentTemplate Template(decimal firstWeight = 50m, decimal secondWeight = 50m)
    {
        var template = new AssessmentTemplate(Guid.NewGuid(), "Fictional readiness", "Original generic assessment.", "admin", Now);
        var category = template.AddCategory("Facility Readiness", 1, "admin", Now);
        template.AddCriterion(category.Id, "FR-01", "Shared area readiness", "Describe a fictional readiness check.", firstWeight, false, 1, "admin", Now);
        template.AddCriterion(category.Id, "FR-02", "Supply organization", "Describe how example supplies are arranged.", secondWeight, true, 2, "admin", Now);
        template.SetBands(Bands, "admin", Now);
        return template;
    }

    [Theory]
    [InlineData("49.99", false)]
    [InlineData("50.00", true)]
    [InlineData("50.01", false)]
    public void Publishing_requires_exactly_one_hundred_percent(string second, bool valid)
    {
        var template = Template(secondWeight: decimal.Parse(second, System.Globalization.CultureInfo.InvariantCulture));
        if (valid)
        {
            template.Publish("admin", Now);
            Assert.Equal(TemplateState.Published, template.State);
        }
        else Assert.Throws<DomainRuleException>(() => template.Publish("admin", Now));
    }

    [Fact]
    public void Published_versions_are_immutable_and_new_draft_has_independent_children()
    {
        var published = Template();
        published.Publish("admin", Now);
        var criterion = published.Criteria.First();
        Assert.Throws<DomainRuleException>(() => published.Rename("Changed", "", "admin", Now));
        Assert.Throws<DomainRuleException>(() => published.UpdateCriterion(criterion.Id, "Changed", "Changed", 25m, true, 0, "admin", Now));
        Assert.Throws<DomainRuleException>(() => published.RemoveCriterion(criterion.Id, "admin", Now));
        Assert.Throws<DomainRuleException>(() => published.RemoveCategory(published.Categories.First().Id, "admin", Now));
        Assert.Throws<DomainRuleException>(() => published.SetBands(Bands, "admin", Now));
        var draft = published.NewVersion(2, "admin", Now);
        draft.UpdateCriterion(draft.Criteria.First().Id, "Revised readiness", "Independent new guidance.", 40m, true, 0, "admin", Now);
        Assert.Equal(50m, criterion.Weight);
        Assert.Equal("Shared area readiness", criterion.Title);
        Assert.Equal(published.FamilyId, draft.FamilyId);
        Assert.Equal(2, draft.Version);
        Assert.Equal(TemplateState.Draft, draft.State);
        Assert.DoesNotContain(draft.Criteria.First().Id, published.Criteria.Select(row => row.Id));
    }

    [Fact]
    public void Draft_categories_and_criteria_can_be_reordered_moved_and_removed_safely()
    {
        var template = Template();
        var originalCategory = template.Categories.Single();
        var secondCategory = template.AddCategory("Record Keeping", 2, "admin", Now);
        var criterion = template.Criteria.First();

        template.UpdateCategory(originalCategory.Id, "Facility Preparation", 3, "admin", Now);
        template.EditCriterion(criterion.Id, secondCategory.Id, "RK-01", "Record readiness",
            "Describe a fictional record check.", 45m, true, 4, "admin", Now);

        Assert.Equal("Facility Preparation", originalCategory.Name);
        Assert.Equal(3, originalCategory.Order);
        Assert.Equal(secondCategory.Id, criterion.CategoryId);
        Assert.Equal("RK-01", criterion.Code);
        Assert.Equal(45m, criterion.Weight);
        Assert.Throws<DomainRuleException>(() => template.RemoveCategory(secondCategory.Id, "admin", Now));

        template.RemoveCriterion(criterion.Id, "admin", Now);
        template.RemoveCategory(secondCategory.Id, "admin", Now);
        Assert.DoesNotContain(template.Criteria, row => row.Id == criterion.Id);
        Assert.DoesNotContain(template.Categories, row => row.Id == secondCategory.Id);
    }

    [Fact]
    public void Codes_bands_and_empty_templates_are_validated()
    {
        var template = Template();
        Assert.Throws<DomainRuleException>(() => template.AddCriterion(template.Categories.First().Id, "fr-01", "Duplicate", "Guidance", 1m, false, 3, "admin", Now));
        Assert.Throws<DomainRuleException>(() => template.SetBands([new("Good", 80m)], "admin", Now));
        Assert.Throws<DomainRuleException>(() => template.SetBands([new("One", 0m), new("Two", 0m)], "admin", Now));
        var empty = new AssessmentTemplate(Guid.NewGuid(), "Empty draft", "", "admin", Now);
        Assert.Throws<DomainRuleException>(() => empty.Publish("admin", Now));
    }

    [Fact]
    public void Scoring_rounds_only_the_final_sum_away_from_zero()
    {
        Assert.Equal(0.005m, WeightedScoring.Contribution(0.01m, 50m));
        Assert.Equal(0.01m, WeightedScoring.Overall([new(0.01m, 50m), new(0m, 50m)]));
        Assert.Equal(0.01m, WeightedScoring.Overall([new(0.01m, 50m), new(0.01m, 50m)]));
        Assert.Equal(82.75m, WeightedScoring.Overall([new(75.5m, 50m), new(90m, 50m)]));
        Assert.Throws<DomainRuleException>(() => WeightedScoring.Overall([new(100m, 99.99m)]));
        Assert.Throws<DomainRuleException>(() => WeightedScoring.Contribution(100.01m, 100m));
        Assert.Throws<DomainRuleException>(() => WeightedScoring.Contribution(0.001m, 100m));
    }

    [Theory]
    [InlineData("0", "Needs Improvement")]
    [InlineData("69.99", "Needs Improvement")]
    [InlineData("70", "Satisfactory")]
    [InlineData("79.99", "Satisfactory")]
    [InlineData("80", "Good")]
    [InlineData("89.99", "Good")]
    [InlineData("90", "Excellent")]
    [InlineData("100", "Excellent")]
    public void Rating_minimums_are_inclusive(string score, string expected)
        => Assert.Equal(expected, WeightedScoring.Rating(decimal.Parse(score, System.Globalization.CultureInfo.InvariantCulture), Bands));

    [Fact]
    public void Ties_share_competition_rank_with_stable_branch_order()
    {
        var ranking = WeightedScoring.Rank([new(Guid.NewGuid(), "Summit Square", 90m), new(Guid.NewGuid(), "Harbor Point", 90m), new(Guid.NewGuid(), "Maple Junction", 80m)]);
        Assert.Equal([1, 1, 3], ranking.Select(row => row.Rank));
        Assert.Equal(["Harbor Point", "Summit Square", "Maple Junction"], ranking.Select(row => row.BranchName));
    }
}
