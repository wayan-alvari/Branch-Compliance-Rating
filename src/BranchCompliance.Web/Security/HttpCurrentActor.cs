using System.Security.Claims;
using BranchCompliance.Application.Security;

namespace BranchCompliance.Web.Security;

public sealed class HttpCurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public Actor Get()
    {
        var user = accessor.HttpContext?.User;
        var id = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = DemoRoles.All.FirstOrDefault(candidate => user?.IsInRole(candidate) == true);
        if (user?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(id) || role is null) throw new AccessDeniedException();
        return new Actor(id, role, user.Identity.Name ?? DemoRoles.Label(role));
    }
}
