using BranchCompliance.Application.Appeals;
using BranchCompliance.Application.Security;
using BranchCompliance.Domain.Rules;
using BranchCompliance.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BranchCompliance.Web.Controllers;

[Authorize(Policy = DemoRoles.BranchUser)]
public sealed class AppealsController(AppealService service) : FormController
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await service.ListAsync(cancellationToken));

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
        => View(await service.DetailsAsync(id, cancellationToken));

    [HttpPost]
    public Task<IActionResult> Create(Guid id, AppealForm model, CancellationToken cancellationToken)
        => SubmitFormAsync(async () =>
        {
            await service.SubmitAsync(id, model.CriterionId!.Value, model.Reason,
                model.Clarification ?? "", cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }, async () => View("Details", await service.DetailsAsync(id, cancellationToken)));

    [HttpPost]
    [RequestSizeLimit(9 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 9 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid id, Guid appealId, IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null) throw new DomainRuleException("Choose one PDF, JPEG, or PNG evidence file.");
        await using var input = file.OpenReadStream();
        await service.UploadEvidenceAsync(id, appealId, input, file.FileName, file.ContentType,
            file.Length, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveEvidence(Guid id, Guid evidenceId, CancellationToken cancellationToken)
    {
        await service.RemoveEvidenceAsync(id, evidenceId, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }
}
