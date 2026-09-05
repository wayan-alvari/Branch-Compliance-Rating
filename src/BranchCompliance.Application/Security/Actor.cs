using BranchCompliance.Domain.Assessments;

namespace BranchCompliance.Application.Security;

public sealed record Actor(string Id, string Role, string DisplayName);
public interface ICurrentActor { Actor Get(); }
public sealed class AccessDeniedException : Exception;
public sealed class ResourceNotFoundException : Exception;
public sealed class ConflictException : Exception;

public static class AccessRules
{
    public static void RequireRole(Actor actor, params string[] roles)
    {
        if (string.IsNullOrWhiteSpace(actor.Id) || !roles.Contains(actor.Role, StringComparer.Ordinal)) throw new AccessDeniedException();
    }

    public static bool CanReadAssessment(Guid workspaceId, Actor actor, BranchAssessment assessment)
        => workspaceId != Guid.Empty && assessment.WorkspaceId == workspaceId && !string.IsNullOrWhiteSpace(actor.Id) && (actor.Role switch
        {
            DemoRoles.Administrator or DemoRoles.Approver => true,
            DemoRoles.BranchUser => assessment.BranchUserId == actor.Id,
            DemoRoles.Assessor => assessment.AssessorId == actor.Id,
            _ => false
        });
}
