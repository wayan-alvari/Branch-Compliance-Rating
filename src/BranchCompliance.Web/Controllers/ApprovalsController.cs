using BranchCompliance.Application.Approval;
using BranchCompliance.Application.Security;
using BranchCompliance.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BranchCompliance.Web.Controllers;

[Authorize(Policy = DemoRoles.Approver)]
public sealed class ApprovalsController(ApprovalService service) : FormController
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await service.ListAsync(cancellationToken));

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
        => View(await service.DetailsAsync(id, cancellationToken));

    [HttpPost]
    public Task<IActionResult> Decide(Guid id, DecisionForm model, CancellationToken cancellationToken)
        => SubmitFormAsync(async () =>
        {
            await service.DecideAsync(id, model.AssessmentId!.Value, model.AppealId!.Value,
                model.Decision == DecisionChoice.Accept, model.Note, model.RevisedScore, cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }, async () => View("Details", await service.DetailsAsync(id, cancellationToken)));

    [HttpPost]
    public async Task<IActionResult> Finalize(Guid id, CancellationToken cancellationToken)
    {
        await service.FinalizeAsync(id, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }
}
