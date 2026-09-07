using System.ComponentModel.DataAnnotations;

namespace BranchCompliance.Web.Models;

public sealed class ScoreForm
{
    [Required] public Guid? CriterionId { get; set; }
    [Required, Range(typeof(decimal), "0", "100")] public decimal? Score { get; set; }
    [StringLength(2000)] public string? Note { get; set; }
}
