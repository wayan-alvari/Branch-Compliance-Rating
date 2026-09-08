using BranchCompliance.Application.Reports;
using BranchCompliance.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BranchCompliance.Web.Controllers;

[Authorize]
public sealed class ReportsController(ReportService service) : Controller
{
    public async Task<IActionResult> Index([FromQuery] ResultsQuery filters, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();
        return View(await service.ListAsync(filters.ToFilter(), cancellationToken));
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
        => View(await service.DetailsAsync(id, cancellationToken));

    public async Task<IActionResult> Spreadsheet([FromQuery] ResultsQuery filters,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();
        var report = await service.ExportSpreadsheetAsync(filters.ToFilter(), cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return File(report.Content, report.MediaType, report.FileName);
    }

    public async Task<IActionResult> BranchSummary(Guid id, CancellationToken cancellationToken)
    {
        var report = await service.ExportBranchSummaryAsync(id, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return File(report.Content, report.MediaType, report.FileName);
    }
}
