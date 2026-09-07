using BranchCompliance.Application.Periods;
using BranchCompliance.Application.Security;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BranchCompliance.Web.Controllers;

[Authorize(Policy = DemoRoles.Administrator)]
public sealed class PeriodsController(PeriodAdministrationService service, IClock clock) : FormController
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await service.PeriodsAsync(cancellationToken));

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
        => View(await service.DetailsAsync(id, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var model = new PeriodCreateForm
        {
            OpensAtUtc = now,
            SubmissionDeadlineUtc = now.AddDays(7),
            AssessmentDeadlineUtc = now.AddDays(14),
            AppealDeadlineUtc = now.AddDays(21),
            FinalizationDeadlineUtc = now.AddDays(28)
        };
        await FillTemplatesAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    public Task<IActionResult> Create(PeriodCreateForm model, CancellationToken cancellationToken)
        => SubmitFormAsync(async () =>
        {
            var id = await service.CreateAsync(model.Name, model.TemplateId!.Value, model.OpensAtUtc,
                model.SubmissionDeadlineUtc, model.AssessmentDeadlineUtc, model.AppealDeadlineUtc,
                model.FinalizationDeadlineUtc, cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }, async () =>
        {
            await FillTemplatesAsync(model, cancellationToken);
            return View(model);
        });

    [HttpPost]
    public Task<IActionResult> Assign(Guid id, PeriodAssignmentForm model, CancellationToken cancellationToken)
        => SubmitFormAsync(async () =>
        {
            await service.AssignAsync(id, model.BranchId!.Value, model.AssessorId, cancellationToken);
            return RedirectToAction(nameof(Details), new { id });
        }, async () => View("Details", await service.DetailsAsync(id, cancellationToken)));

    [HttpPost]
    public async Task<IActionResult> Open(Guid id, CancellationToken cancellationToken)
    {
        await service.OpenAsync(id, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Advance(Guid id, CancellationToken cancellationToken)
    {
        await service.AdvanceAsync(id, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task FillTemplatesAsync(PeriodCreateForm model, CancellationToken cancellationToken)
    {
        model.Templates = (await service.PublishedTemplatesAsync(cancellationToken))
            .Select(row => new SelectListItem($"{row.Name} · version {row.Version}", row.Id.ToString()))
            .ToArray();
    }
}
