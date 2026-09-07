using BranchCompliance.Domain.Rules;
using Microsoft.AspNetCore.Mvc;

namespace BranchCompliance.Web.Controllers;

public abstract class FormController : Controller
{
    protected async Task<IActionResult> SubmitFormAsync(Func<Task<IActionResult>> command, Func<Task<IActionResult>> invalid)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return await invalid();
        }
        try { return await command(); }
        catch (DomainRuleException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return await invalid();
        }
    }
}
