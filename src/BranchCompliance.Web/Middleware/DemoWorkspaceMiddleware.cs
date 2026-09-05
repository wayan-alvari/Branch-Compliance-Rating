using System.Security.Cryptography;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Infrastructure.Workspaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace BranchCompliance.Web.Middleware;

public sealed class DemoWorkspaceMiddleware(RequestDelegate next)
{
    public const string CookieName = "BranchCompliance.DemoWorkspaceId";

    public async Task InvokeAsync(HttpContext http, IConfiguration configuration, IDataProtectionProvider protection,
        WorkspaceContext context, WorkspaceCoordinator coordinator, IWorkspaceLifecycle lifecycle, IClock clock)
    {
        var action = http.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (action is null || action.ControllerName == "Home" && action.ActionName is "Error" or "Status")
        {
            await next(http);
            return;
        }
        if (!configuration.GetValue<bool>("DemoMode:Enabled"))
        {
            if (Guid.TryParse(configuration["Workspace:Id"], out var fixedWorkspace)) context.Activate(fixedWorkspace);
            await next(http);
            return;
        }
        using var lease = await coordinator.EnterAsync(http.RequestAborted);
        var protector = protection.CreateProtector("BranchCompliance.DemoWorkspace.v1");
        Guid? previous = null;
        if (http.Request.Cookies.TryGetValue(CookieName, out var cookie))
        {
            try
            {
                if (Guid.TryParse(protector.Unprotect(cookie), out var id) && id != Guid.Empty) previous = id;
            }
            catch (CryptographicException) { /* An invalid cookie never selects an existing workspace. */ }
        }
        var workspaceId = await lifecycle.ResolveAsync(previous, http.User.Identity?.IsAuthenticated == true, http.RequestAborted);
        if (previous != workspaceId)
        {
            http.Response.Cookies.Append(CookieName, protector.Protect(workspaceId.ToString("N")), new CookieOptions
            {
                HttpOnly = true,
                Secure = http.Request.IsHttps || !http.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment(),
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Path = "/",
                Expires = new DateTimeOffset(clock.UtcNow.AddDays(30))
            });
        }
        await next(http);
    }
}
