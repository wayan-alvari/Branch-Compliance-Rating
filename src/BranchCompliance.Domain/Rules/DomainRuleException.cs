namespace BranchCompliance.Domain.Rules;

public sealed class DomainRuleException(string message) : Exception(message);

public static class Rule
{
    public static void Require(bool condition, string message)
    {
        if (!condition) throw new DomainRuleException(message);
    }

    public static string Text(string? value, string label, int maximum, bool required = true)
    {
        var text = value?.Trim() ?? "";
        Require((!required || text.Length > 0) && text.Length <= maximum,
            $"{label} {(required ? "is required and " : "")}must be no longer than {maximum} characters.");
        return text;
    }

    public static decimal Percentage(decimal value, string label, bool positive = false)
    {
        Require(value >= 0m && value <= 100m && (!positive || value > 0m) && decimal.Round(value, 2) == value,
            $"{label} must be {(positive ? "greater than 0" : "at least 0")} and at most 100, with up to two decimal places.");
        return value;
    }

    public static void Utc(DateTime value) => Require(value.Kind == DateTimeKind.Utc, "Use a UTC timestamp.");
}
