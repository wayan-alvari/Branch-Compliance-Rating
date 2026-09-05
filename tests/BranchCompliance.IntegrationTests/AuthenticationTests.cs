using System.Net;
using BranchCompliance.Application.Security;
using BranchCompliance.Infrastructure.Identity;
using BranchCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BranchCompliance.IntegrationTests;

public sealed class AuthenticationTests(ComplianceWebFactory factory) : IClassFixture<ComplianceWebFactory>
{
    [Theory]
    [InlineData("admin@compliance.demo", DemoRoles.Administrator)]
    [InlineData("branch@compliance.demo", DemoRoles.BranchUser)]
    [InlineData("assessor@compliance.demo", DemoRoles.Assessor)]
    [InlineData("approver@compliance.demo", DemoRoles.Approver)]
    public async Task Demo_login_enforces_all_role_policies_and_logout(string email, string role)
    {
        using var browser = factory.Browser();
        var login = await ComplianceWebFactory.LoginAsync(browser, email);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/Dashboard", login.Headers.Location?.OriginalString);
        var page = await browser.GetStringAsync("/Dashboard");
        Assert.Contains(email, page);
        Assert.Contains(DemoRoles.Label(role), page);

        foreach (var policy in DemoRoles.All)
        {
            var result = await browser.GetAsync($"/test-policy/{policy}");
            Assert.Equal(policy == role ? HttpStatusCode.OK : HttpStatusCode.Redirect, result.StatusCode);
            if (policy != role)
            {
                Assert.Contains("/Account/AccessDenied", result.Headers.Location?.OriginalString);
            }
        }

        var token = await ComplianceWebFactory.TokenAsync(browser, "/Dashboard");
        var logout = await browser.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        var denied = await browser.GetAsync("/Dashboard");
        Assert.Contains("/Account/Login", denied.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Anonymous_dashboard_requires_authentication_and_credentials_are_visible()
    {
        using var browser = factory.Browser();
        var protectedPage = await browser.GetAsync("/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
        var login = await browser.GetAsync("/Account/Login");
        var html = await login.Content.ReadAsStringAsync();
        foreach (var account in DemoIdentitySeeder.Accounts)
        {
            Assert.Contains(account.Email, html);
        }
        Assert.Contains(DemoIdentitySeeder.Password, html);
        Assert.Contains("automatically resets after 6 hours of inactivity", html);
        Assert.Contains("frame-ancestors 'none'", login.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", login.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Invalid_credentials_do_not_authenticate_and_external_redirect_is_discarded()
    {
        using var browser = factory.Browser();
        var invalid = await ComplianceWebFactory.LoginAsync(browser, "branch@compliance.demo", "InvalidExample123!");
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.Contains("Sign-in failed", await invalid.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/Dashboard")).StatusCode);
        var valid = await ComplianceWebFactory.LoginAsync(browser, "branch@compliance.demo", returnUrl: "https://example.invalid/elsewhere");
        Assert.Equal("/Dashboard", valid.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Changes_require_antiforgery_and_identity_management_is_absent()
    {
        using var browser = factory.Browser();
        var login = await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "admin@compliance.demo",
            ["Password"] = DemoIdentitySeeder.Password
        }));
        Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);
        await ComplianceWebFactory.LoginAsync(browser, "admin@compliance.demo");
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync("/Account/Logout", new FormUrlEncodedContent([]))).StatusCode);
        foreach (var path in new[] { "/Account/Register", "/Identity/Account/Register", "/Account/ChangePassword", "/Account/ManageRoles" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync(path)).StatusCode);
        }
    }

    [Fact]
    public async Task Identity_seed_is_idempotent_and_health_is_public()
    {
        using var browser = factory.Browser();
        Assert.Equal("Healthy", await browser.GetStringAsync("/health"));
        await using var scope = factory.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoIdentitySeeder>();
        await seeder.SeedAsync(CancellationToken.None);
        await seeder.SeedAsync(CancellationToken.None);
        var db = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();
        Assert.Equal(4, await db.Users.CountAsync());
        Assert.Equal(4, await db.Roles.CountAsync());
        Assert.Equal(4, await db.UserRoles.CountAsync());
    }
}
