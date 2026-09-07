using System.ComponentModel.DataAnnotations;

namespace BranchCompliance.Web.Models;

public sealed class AppealForm
{
    [Required] public Guid? CriterionId { get; set; }
    [Required, StringLength(2000)] public string Reason { get; set; } = "";
    [StringLength(2000)] public string? Clarification { get; set; }
}
