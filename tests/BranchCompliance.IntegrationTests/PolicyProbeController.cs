using BranchCompliance.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BranchCompliance.IntegrationTests;

// Included only by the test factory, never by the production application.
public sealed class PolicyProbeController : Controller
{
    [HttpGet("/test-policy/Administrator"), Authorize(Policy = DemoRoles.Administrator)]
    public IActionResult Administrator() => Ok();

    [HttpGet("/test-policy/BranchUser"), Authorize(Policy = DemoRoles.BranchUser)]
    public IActionResult BranchUser() => Ok();

    [HttpGet("/test-policy/Assessor"), Authorize(Policy = DemoRoles.Assessor)]
    public IActionResult Assessor() => Ok();

    [HttpGet("/test-policy/Approver"), Authorize(Policy = DemoRoles.Approver)]
    public IActionResult Approver() => Ok();
}
