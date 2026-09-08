using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BranchCompliance.Web.Controllers;

[AllowAnonymous]
public sealed class HomeController : Controller
{
    public IActionResult Index() => RedirectToAction("Login", "Account");

    [Route("/Home/Error")]
    public IActionResult Error()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View("Problem", "Something went wrong. Please try again.");
    }

    [Route("/Home/Status/{code:int}")]
    public IActionResult Status(int code)
    {
        var safeCode = code is >= 400 and <= 599 ? code : StatusCodes.Status404NotFound;
        Response.StatusCode = safeCode;
        var message = safeCode switch
        {
            StatusCodes.Status404NotFound => "This page could not be found.",
            StatusCodes.Status429TooManyRequests => "Too many requests were received. Wait a moment and try again.",
            _ => "The request could not be completed."
        };
        return View("Problem", message);
    }
}
