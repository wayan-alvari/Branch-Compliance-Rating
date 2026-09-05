using BranchCompliance.Application.Security;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BranchCompliance.IntegrationTests;

// Included only by the test factory, never by the production application.
public sealed class PolicyProbeController : Controller
{
    [HttpGet("/test-workspace"), Authorize]
    public async Task<IActionResult> Workspace([FromServices] IWorkspaceContext workspace, [FromServices] ComplianceDbContext db)
        => Ok(new { workspace.WorkspaceId, AuditIds = await db.AuditEvents.Where(row => row.Action == "Workspace created").Select(row => row.Id).ToArrayAsync() });

    [HttpGet("/test-workspace/audit/{id:guid}"), Authorize]
    public async Task<IActionResult> Audit(Guid id, [FromServices] ComplianceDbContext db)
        => await db.AuditEvents.AnyAsync(row => row.Id == id) ? Ok() : NotFound();

    [HttpGet("/test-policy/Administrator"), Authorize(Policy = DemoRoles.Administrator)]
    public IActionResult Administrator() => Ok();

    [HttpGet("/test-policy/BranchUser"), Authorize(Policy = DemoRoles.BranchUser)]
    public IActionResult BranchUser() => Ok();

    [HttpGet("/test-policy/Assessor"), Authorize(Policy = DemoRoles.Assessor)]
    public IActionResult Assessor() => Ok();

    [HttpGet("/test-policy/Approver"), Authorize(Policy = DemoRoles.Approver)]
    public IActionResult Approver() => Ok();
}
