using BranchCompliance.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BranchCompliance.Web.Controllers;

[Authorize]
public sealed class DashboardController(DashboardService service) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await service.GetAsync(cancellationToken));
}
