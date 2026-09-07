using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BranchCompliance.Web.Models;

public sealed class BranchForm
{
    public Guid? Id { get; set; }
    [Required, StringLength(24), RegularExpression("[A-Za-z0-9-]+")]
    public string Code { get; set; } = "";
    [Required, StringLength(120)] public string Name { get; set; } = "";
    [Required, StringLength(80)] public string Region { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public bool AssignDemoUser { get; set; } = true;
}

public sealed class TemplateForm
{
    public Guid? Id { get; set; }
    [Required, StringLength(120)] public string Name { get; set; } = "";
    [StringLength(1000)] public string Description { get; set; } = "";
}

public sealed class CategoryForm
{
    public Guid TemplateId { get; set; }
    public Guid? Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Range(0, 999)] public int Order { get; set; }
}

public sealed class CriterionForm
{
    public Guid TemplateId { get; set; }
    public Guid? Id { get; set; }
    [Required] public Guid CategoryId { get; set; }
    [Required, StringLength(24), RegularExpression("[A-Za-z0-9-]+")] public string Code { get; set; } = "";
    [Required, StringLength(160)] public string Title { get; set; } = "";
    [Required, StringLength(2000)] public string Guidance { get; set; } = "";
    [Range(typeof(decimal), "0.01", "100")] public decimal Weight { get; set; }
    public bool EvidenceRequired { get; set; }
    [Range(0, 999)] public int Order { get; set; }
    [BindNever] public IReadOnlyList<SelectListItem> Categories { get; set; } = [];
}

public sealed class BandsForm
{
    public Guid TemplateId { get; set; }
    [MinLength(1), MaxLength(10)] public List<BandRow> Bands { get; set; } = [];
}
public sealed class BandRow
{
    [Required, StringLength(60)] public string Label { get; set; } = "";
    [Range(typeof(decimal), "0", "100")] public decimal Minimum { get; set; }
}

public sealed class PeriodCreateForm
{
    [Required, StringLength(120)] public string Name { get; set; } = "";
    [Required] public Guid? TemplateId { get; set; }
    [Display(Name = "Opens at (UTC)")] public DateTime OpensAtUtc { get; set; }
    [Display(Name = "Submission deadline (UTC)")] public DateTime SubmissionDeadlineUtc { get; set; }
    [Display(Name = "Assessment deadline (UTC)")] public DateTime AssessmentDeadlineUtc { get; set; }
    [Display(Name = "Appeal deadline (UTC)")] public DateTime AppealDeadlineUtc { get; set; }
    [Display(Name = "Finalization deadline (UTC)")] public DateTime FinalizationDeadlineUtc { get; set; }
    [BindNever] public IReadOnlyList<SelectListItem> Templates { get; set; } = [];
}

public sealed class PeriodAssignmentForm
{
    [Required] public Guid? BranchId { get; set; }
    [Required] public string AssessorId { get; set; } = "";
}
