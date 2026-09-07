using BranchCompliance.Application.Configuration;
using BranchCompliance.Application.Security;
using BranchCompliance.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BranchCompliance.Web.Controllers;

[Authorize(Policy = DemoRoles.Administrator)]
public sealed class BranchesController(ConfigurationService service) : FormController
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await service.BranchesAsync(cancellationToken));

    [HttpGet]
    public IActionResult Create() => View("Form", new BranchForm());

    [HttpPost]
    public Task<IActionResult> Create(BranchForm model, CancellationToken cancellationToken) => SubmitFormAsync(async () =>
    {
        await service.CreateBranchAsync(model.Code, model.Name, model.Region, model.AssignDemoUser, cancellationToken);
        return RedirectToAction(nameof(Index));
    }, () => Task.FromResult<IActionResult>(View("Form", model)));

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var branch = await service.BranchAsync(id, cancellationToken);
        return View("Form", new BranchForm { Id = branch.Id, Code = branch.Code, Name = branch.Name, Region = branch.Region, IsActive = branch.IsActive, AssignDemoUser = branch.BranchUserId is not null });
    }

    [HttpPost]
    public Task<IActionResult> Edit(Guid id, BranchForm model, CancellationToken cancellationToken) => SubmitFormAsync(async () =>
    {
        await service.EditBranchAsync(id, model.Name, model.Region, model.IsActive, model.AssignDemoUser, cancellationToken);
        return RedirectToAction(nameof(Index));
    }, () => Task.FromResult<IActionResult>(View("Form", model)));
}
