using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BranchCompliance.Web.Controllers;

[Authorize]
public sealed class DashboardController : Controller
{
    public IActionResult Index() => View();
}
