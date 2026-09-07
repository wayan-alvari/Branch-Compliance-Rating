using System.ComponentModel.DataAnnotations;

namespace BranchCompliance.Web.Models;

public sealed class ResponseForm
{
    [Required] public Guid? CriterionId { get; set; }
    [StringLength(2000)] public string? Answer { get; set; }
    [StringLength(2000)] public string? Comment { get; set; }
}
