namespace BranchCompliance.Application.Security;

public static class DemoRoles
{
    public const string Administrator = "Administrator";
    public const string BranchUser = "BranchUser";
    public const string Assessor = "Assessor";
    public const string Approver = "Approver";
    public static readonly string[] All = [Administrator, BranchUser, Assessor, Approver];
    public static string Label(string role) => role == BranchUser ? "Branch User" : role;
}
