using BranchCompliance.Application.Security;
using BranchCompliance.Domain.Rules;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;

namespace BranchCompliance.Web.Filters;

public sealed class ApplicationExceptionFilter(ILogger<ApplicationExceptionFilter> logger) : IExceptionFilter
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
        logger.LogInformation(new EventId(1001, "ExpectedRequestFailure"),
            "Request rejected with status {StatusCode} by {Action}; failure type {FailureType}.",
            problem.Item1, context.ActionDescriptor.DisplayName ?? "unknown action",
            context.Exception.GetType().Name);
        context.Result = new ViewResult
        {
            ViewName = "~/Views/Home/Problem.cshtml",
            StatusCode = problem.Item1,
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = problem.Item2 }
        };
        context.ExceptionHandled = true;
    }
}
