using System.ComponentModel.DataAnnotations;

namespace BranchCompliance.Web.Models;

public enum DecisionChoice { Accept, Reject }

public sealed class DecisionForm
{
    [Required] public Guid? AssessmentId { get; set; }
    [Required] public Guid? AppealId { get; set; }
    [Required] public DecisionChoice? Decision { get; set; }
    [Required, StringLength(2000)] public string Note { get; set; } = "";
    [Range(typeof(decimal), "0", "100")] public decimal? RevisedScore { get; set; }
}
