using BranchCompliance.Application.Security;
using BranchCompliance.Application.Submissions;
using BranchCompliance.Domain.Rules;
using BranchCompliance.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BranchCompliance.Web.Controllers;

[Authorize(Roles = DemoRoles.Administrator + "," + DemoRoles.BranchUser)]
public sealed class SubmissionsController(SubmissionService service) : FormController
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await service.ListAsync(cancellationToken));

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
        => View(await service.DetailsAsync(id, cancellationToken));

    [HttpPost]
    public Task<IActionResult> Save(Guid id, ResponseForm model, CancellationToken cancellationToken)
        => SubmitFormAsync(async () =>
        {
            await service.SaveResponseAsync(id, model.CriterionId!.Value, model.Answer ?? "", model.Comment ?? "", cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }, async () => View("Details", await service.DetailsAsync(id, cancellationToken)));

    [HttpPost]
    [RequestSizeLimit(9 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 9 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid id, Guid responseId, IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null) throw new DomainRuleException("Choose one PDF, JPEG, or PNG evidence file.");
        await using var input = file.OpenReadStream();
        await service.UploadResponseEvidenceAsync(id, responseId, input, file.FileName,
            file.ContentType, file.Length, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveEvidence(Guid id, Guid evidenceId, CancellationToken cancellationToken)
    {
        await service.RemoveEvidenceAsync(id, evidenceId, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        await service.SubmitAsync(id, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }
}
