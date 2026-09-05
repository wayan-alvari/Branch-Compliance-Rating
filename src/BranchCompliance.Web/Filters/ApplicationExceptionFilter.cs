using BranchCompliance.Application.Security;
using BranchCompliance.Domain.Rules;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace BranchCompliance.Web.Filters;

public sealed class ApplicationExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var problem = context.Exception switch
        {
            AccessDeniedException => (403, "Your current role or assignment cannot perform this action."),
            ResourceNotFoundException => (404, "This record could not be found in your workspace."),
            ConflictException => (409, "This record changed in another request. Reload the page and try again."),
            DomainRuleException rule => (400, rule.Message),
            _ => (0, "")
        };
        if (problem.Item1 == 0) return;
        context.Result = new ViewResult
        {
            ViewName = "~/Views/Home/Problem.cshtml",
            StatusCode = problem.Item1,
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = problem.Item2 }
        };
        context.ExceptionHandled = true;
    }
}
