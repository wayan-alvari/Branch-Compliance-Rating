using BranchCompliance.Application.Configuration;
using BranchCompliance.Application.Security;
using BranchCompliance.Domain.Scoring;
using BranchCompliance.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BranchCompliance.Web.Controllers;

[Authorize(Policy = DemoRoles.Administrator)]
public sealed class TemplatesController(ConfigurationService service) : FormController
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await service.TemplatesAsync(cancellationToken));
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken) => View(await service.TemplateAsync(id, cancellationToken));

    [HttpGet] public IActionResult Create() => View("Form", new TemplateForm());
    [HttpPost]
    public Task<IActionResult> Create(TemplateForm model, CancellationToken cancellationToken) => SubmitFormAsync(async () =>
        RedirectToAction(nameof(Details), new { id = await service.CreateTemplateAsync(model.Name, model.Description, cancellationToken) }),
        () => Task.FromResult<IActionResult>(View("Form", model)));

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var template = await service.TemplateAsync(id, cancellationToken);
        return View("Form", new TemplateForm { Id = template.Id, Name = template.Name, Description = template.Description });
    }
    [HttpPost]
    public Task<IActionResult> Edit(Guid id, TemplateForm model, CancellationToken cancellationToken) => SubmitFormAsync(async () =>
    {
        await service.RenameTemplateAsync(id, model.Name, model.Description, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }, () => Task.FromResult<IActionResult>(View("Form", model)));

    [HttpPost]
    public async Task<IActionResult> NewVersion(Guid id, CancellationToken cancellationToken)
        => RedirectToAction(nameof(Details), new { id = await service.NewVersionAsync(id, cancellationToken) });
    [HttpPost]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        await service.PublishAsync(id, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Category(Guid templateId, Guid? id, CancellationToken cancellationToken)
    {
        var template = await service.TemplateAsync(templateId, cancellationToken);
        var category = id is null ? null : template.Categories.SingleOrDefault(row => row.Id == id) ?? throw new ResourceNotFoundException();
        return View(new CategoryForm { TemplateId = templateId, Id = id, Name = category?.Name ?? "", Order = category?.Order ?? 0 });
    }
    [HttpPost]
    public Task<IActionResult> Category(CategoryForm model, CancellationToken cancellationToken) => SubmitFormAsync(async () =>
    {
        await service.SaveCategoryAsync(model.TemplateId, model.Id, model.Name, model.Order, cancellationToken);
        return RedirectToAction(nameof(Details), new { id = model.TemplateId });
    }, () => Task.FromResult<IActionResult>(View(model)));
    [HttpPost]
    public async Task<IActionResult> RemoveCategory(Guid templateId, Guid id, CancellationToken cancellationToken)
    {
        await service.RemoveCategoryAsync(templateId, id, cancellationToken);
        return RedirectToAction(nameof(Details), new { id = templateId });
    }

    [HttpGet]
    public async Task<IActionResult> Criterion(Guid templateId, Guid? id, CancellationToken cancellationToken)
    {
        var template = await service.TemplateAsync(templateId, cancellationToken);
        var criterion = id is null ? null : template.Criteria.SingleOrDefault(row => row.Id == id) ?? throw new ResourceNotFoundException();
        var model = new CriterionForm
        {
            TemplateId = templateId,
            Id = id,
            CategoryId = criterion?.CategoryId ?? template.Categories.FirstOrDefault()?.Id ?? Guid.Empty,
            Code = criterion?.Code ?? "",
            Title = criterion?.Title ?? "",
            Guidance = criterion?.Guidance ?? "",
            Weight = criterion?.Weight ?? 10m,
            EvidenceRequired = criterion?.EvidenceRequired ?? false,
            Order = criterion?.Order ?? 0
        };
        await FillCategories(model, cancellationToken);
        return View(model);
    }
    [HttpPost]
    public Task<IActionResult> Criterion(CriterionForm model, CancellationToken cancellationToken) => SubmitFormAsync(async () =>
    {
        await service.SaveCriterionAsync(model.TemplateId, model.Id, model.CategoryId, model.Code, model.Title, model.Guidance, model.Weight, model.EvidenceRequired, model.Order, cancellationToken);
        return RedirectToAction(nameof(Details), new { id = model.TemplateId });
    }, async () => { await FillCategories(model, cancellationToken); return View(model); });
    [HttpPost]
    public async Task<IActionResult> RemoveCriterion(Guid templateId, Guid id, CancellationToken cancellationToken)
    {
        await service.RemoveCriterionAsync(templateId, id, cancellationToken);
        return RedirectToAction(nameof(Details), new { id = templateId });
    }

    [HttpGet]
    public async Task<IActionResult> Bands(Guid id, CancellationToken cancellationToken)
    {
        var template = await service.TemplateAsync(id, cancellationToken);
        return View(new BandsForm { TemplateId = id, Bands = template.Bands.OrderBy(row => row.Order).Select(row => new BandRow { Label = row.Label, Minimum = row.MinimumInclusive }).ToList() });
    }
    [HttpPost]
    public Task<IActionResult> Bands(BandsForm model, CancellationToken cancellationToken) => SubmitFormAsync(async () =>
    {
        await service.SetBandsAsync(model.TemplateId, model.Bands.Select(row => new RatingThreshold(row.Label, row.Minimum)).ToArray(), cancellationToken);
        return RedirectToAction(nameof(Details), new { id = model.TemplateId });
    }, () =>
    {
        if (model.Bands.Count == 0) model.Bands.Add(new BandRow());
        return Task.FromResult<IActionResult>(View(model));
    });

    private async Task FillCategories(CriterionForm model, CancellationToken cancellationToken)
        => model.Categories = (await service.TemplateAsync(model.TemplateId, cancellationToken)).Categories.OrderBy(row => row.Order)
            .Select(row => new SelectListItem(row.Name, row.Id.ToString())).ToArray();
}
