using BranchCompliance.Application.Security;
using Microsoft.AspNetCore.Identity;

namespace BranchCompliance.Infrastructure.Identity;

public sealed class DemoIdentitySeeder(UserManager<DemoUser> users, RoleManager<IdentityRole> roles)
{
    public const string Password = "PortfolioDemo123!";
    public static readonly (string Role, string Email)[] Accounts =
    [
        (DemoRoles.Administrator, "admin@compliance.demo"),
        (DemoRoles.BranchUser, "branch@compliance.demo"),
        (DemoRoles.Assessor, "assessor@compliance.demo"),
        (DemoRoles.Approver, "approver@compliance.demo")
    ];

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        foreach (var (role, email) in Accounts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await roles.RoleExistsAsync(role))
            {
                Ensure(await roles.CreateAsync(new IdentityRole(role)));
            }

            var user = await users.FindByEmailAsync(email);
            if (user is null)
            {
                user = new DemoUser { UserName = email, Email = email, EmailConfirmed = true, LockoutEnabled = false };
                Ensure(await users.CreateAsync(user, Password));
            }
            if (!await users.IsInRoleAsync(user, role))
            {
                Ensure(await users.AddToRoleAsync(user, role));
            }
        }
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Demo identity initialization failed. Review Identity configuration.");
        }
    }
}
