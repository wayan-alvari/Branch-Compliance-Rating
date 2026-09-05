using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BranchCompliance.Web.Controllers;

[AllowAnonymous]
public sealed class HomeController : Controller
{
    public IActionResult Index() => RedirectToAction("Login", "Account");

    [Route("/Home/Error")]
    public IActionResult Error() => View("Problem", "Something went wrong. Please try again.");

    [Route("/Home/Status/{code:int}")]
    public IActionResult Status(int code)
    {
        Response.StatusCode = code;
        return View("Problem", code == 404 ? "This page could not be found." : "The request could not be completed.");
    }
}
