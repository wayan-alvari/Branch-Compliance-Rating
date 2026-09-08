using System.ComponentModel.DataAnnotations;
using BranchCompliance.Application.Reports;
using BranchCompliance.Domain.Assessments;

namespace BranchCompliance.Web.Models;

public sealed class ResultsQuery
{
    public Guid? PeriodId { get; set; }
    [StringLength(120)] public string Query { get; set; } = "";
    [StringLength(60)] public string Rating { get; set; } = "";
    public AssessmentState? Status { get; set; }

    public ResultFilter ToFilter() => new(PeriodId, Query, Rating, Status);
}

public sealed class AuditQuery
{
    [StringLength(120)] public string Query { get; set; } = "";
    [StringLength(120)] public string Actor { get; set; } = "";
    [DataType(DataType.Date)] public DateOnly? From { get; set; }
    [DataType(DataType.Date)] public DateOnly? To { get; set; }

    public AuditFilter ToFilter() => new(Query, Actor,
        From?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
        ExclusiveUtc(To));

    private static DateTime? ExclusiveUtc(DateOnly? value)
        => value switch
        {
            null => null,
            DateOnly date when date == DateOnly.MaxValue => DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc),
            DateOnly date => date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        };
}
