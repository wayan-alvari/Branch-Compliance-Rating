using BranchCompliance.Domain.Rules;

namespace BranchCompliance.Domain.Scoring;

public sealed record WeightedScore(decimal Score, decimal Weight);
public sealed record RatingThreshold(string Label, decimal MinimumInclusive);
public sealed record RankingInput(Guid AssessmentId, string BranchName, decimal Score);
public sealed record RankingEntry(Guid AssessmentId, string BranchName, decimal Score, int Rank);

public static class WeightedScoring
{
    public static decimal Contribution(decimal score, decimal weight)
        => Rule.Percentage(score, "Score") * (Rule.Percentage(weight, "Weight", positive: true) / 100m);

    public static decimal Overall(IEnumerable<WeightedScore> values)
    {
        var scores = values.ToArray();
        Rule.Require(scores.Length > 0 && scores.Sum(row => row.Weight) == 100.00m, "Criterion weights must total exactly 100.00.");
        return decimal.Round(scores.Sum(row => Contribution(row.Score, row.Weight)), 2, MidpointRounding.AwayFromZero);
    }

    public static string Rating(decimal overall, IEnumerable<RatingThreshold> bands)
    {
        Rule.Percentage(overall, "Overall score");
        var ordered = bands.OrderByDescending(band => band.MinimumInclusive).ToArray();
        ValidateBands(ordered);
        return ordered.First(band => overall >= band.MinimumInclusive).Label;
    }

    public static void ValidateBands(IEnumerable<RatingThreshold> bands)
    {
        var rows = bands.ToArray();
        Rule.Require(rows.Length > 0 && rows.Any(row => row.MinimumInclusive == 0m), "Rating bands must include a minimum of 0.00.");
        Rule.Require(rows.Select(row => row.MinimumInclusive).Distinct().Count() == rows.Length, "Rating thresholds must be unique.");
        Rule.Require(rows.Select(row => row.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count() == rows.Length, "Rating labels must be unique.");
        foreach (var row in rows)
        {
            Rule.Text(row.Label, "Rating label", 60);
            Rule.Percentage(row.MinimumInclusive, "Rating threshold");
        }
    }

    public static IReadOnlyList<RankingEntry> Rank(IEnumerable<RankingInput> input)
    {
        var rows = input.OrderByDescending(row => row.Score).ThenBy(row => row.BranchName, StringComparer.Ordinal)
            .ThenBy(row => row.AssessmentId).ToArray();
        var result = new List<RankingEntry>();
        var rank = 0;
        decimal? previous = null;
        for (var i = 0; i < rows.Length; i++)
        {
            Rule.Percentage(rows[i].Score, "Final score");
            if (previous != rows[i].Score) rank = i + 1;
            result.Add(new RankingEntry(rows[i].AssessmentId, rows[i].BranchName, rows[i].Score, rank));
            previous = rows[i].Score;
        }
        return result;
    }
}
