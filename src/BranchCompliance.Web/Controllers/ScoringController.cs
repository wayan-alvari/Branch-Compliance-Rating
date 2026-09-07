using BranchCompliance.Application.Scoring;
using BranchCompliance.Application.Security;
using BranchCompliance.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BranchCompliance.Web.Controllers;

[Authorize(Policy = DemoRoles.Assessor)]
public sealed class ScoringController(ScoringService service) : FormController
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await service.ListAsync(cancellationToken));

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
        => View(await service.DetailsAsync(id, cancellationToken));

    [HttpPost]
    public Task<IActionResult> Save(Guid id, ScoreForm model, CancellationToken cancellationToken)
        => SubmitFormAsync(async () =>
        {
            await service.SaveScoreAsync(id, model.CriterionId!.Value, model.Score!.Value,
                model.Note ?? "", cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }, async () => View("Details", await service.DetailsAsync(id, cancellationToken)));

    [HttpPost]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        await service.CompleteAsync(id, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }
}
