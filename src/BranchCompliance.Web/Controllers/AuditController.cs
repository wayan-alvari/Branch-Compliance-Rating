using BranchCompliance.Application.Reports;
using BranchCompliance.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BranchCompliance.Web.Controllers;

[Authorize]
public sealed class AuditController(ReportService service) : Controller
{
    public async Task<IActionResult> Index([FromQuery] AuditQuery filters, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();
        return View(await service.AuditAsync(filters.ToFilter(), cancellationToken));
    }
}
