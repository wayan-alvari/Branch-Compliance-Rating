using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Domain.Assessments;
using BranchCompliance.Domain.Periods;
using BranchCompliance.Domain.Rules;
using BranchCompliance.Infrastructure.Persistence;
using BranchCompliance.Infrastructure.Workspaces;
using BranchCompliance.Web.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BranchCompliance.IntegrationTests;

public sealed class HardeningTests
{
    private sealed record WorkspaceInfo(Guid WorkspaceId, Guid[] AuditIds);
    private sealed record PageIds(Guid HistoricalPeriodId, Guid HarborCurrentId, Guid MapleCurrentId);

    private static async Task<Guid> Workspace(HttpClient browser)
        => (await browser.GetFromJsonAsync<WorkspaceInfo>("/test-workspace"))!.WorkspaceId;

    private static async Task<PageIds> Ids(ComplianceWebFactory factory, Guid workspace)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<WorkspaceContext>().Activate(workspace);
        var db = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();
        var historical = await db.Periods.SingleAsync(row => row.Phase == PeriodPhase.Finalized);
        var current = await db.Periods.SingleAsync(row => row.Phase == PeriodPhase.SubmissionOpen);
        var assessments = await db.Assessments.Where(row => row.PeriodId == current.Id).ToArrayAsync();
        return new PageIds(historical.Id,
            assessments.Single(row => row.BranchCode == "DEMO-HP").Id,
            assessments.Single(row => row.BranchCode == "DEMO-MJ").Id);
    }

    private static async Task SwitchRoleAsync(HttpClient browser, string email)
    {
        Assert.Equal(HttpStatusCode.Redirect,
            (await ComplianceWebFactory.PostAsync(browser, "/Dashboard", "/Account/Logout", new())).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await ComplianceWebFactory.LoginAsync(browser, email)).StatusCode);
    }

    [Fact]
    public async Task Liveness_is_minimal_cache_safe_and_does_not_create_a_workspace()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        var health = await browser.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("text/plain", health.Content.Headers.ContentType!.MediaType);
        Assert.Equal("Healthy", await health.Content.ReadAsStringAsync());
        Assert.True(health.Headers.CacheControl!.NoStore);
        Assert.False(health.Headers.TryGetValues("Set-Cookie", out _));
        AssertSecurityHeaders(health);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ComplianceDbContext>()
            .Workspaces.IgnoreQueryFilters().ToArrayAsync());
    }

    [Fact]
    public async Task Dynamic_pages_and_cookies_use_defensive_browser_policies()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        var loginPage = await browser.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
        Assert.True(loginPage.Headers.CacheControl!.NoStore);
        Assert.Equal("no-cache", loginPage.Headers.Pragma.ToString());
        AssertSecurityHeaders(loginPage);
        var cookies = loginPage.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(cookies, cookie => cookie.StartsWith("BranchCompliance.DemoWorkspaceId=", StringComparison.Ordinal) &&
            cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
            cookie.Contains("secure", StringComparison.OrdinalIgnoreCase) &&
            cookie.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cookies, cookie => cookie.StartsWith("BranchCompliance.Antiforgery=", StringComparison.Ordinal) &&
            cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
            cookie.Contains("secure", StringComparison.OrdinalIgnoreCase) &&
            cookie.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));

        var signIn = await ComplianceWebFactory.LoginAsync(browser, "admin@compliance.demo");
        var identityCookie = signIn.Headers.GetValues("Set-Cookie").Single(row =>
            row.StartsWith("BranchCompliance.Identity=", StringComparison.Ordinal));
        Assert.Contains("httponly", identityCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", identityCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", identityCookie, StringComparison.OrdinalIgnoreCase);
        var dashboard = await browser.GetAsync("/Dashboard");
        Assert.True(dashboard.Headers.CacheControl!.NoStore);
        AssertSecurityHeaders(dashboard);

        var forms = factory.Services.GetRequiredService<IOptions<FormOptions>>().Value;
        Assert.Equal(9 * 1024 * 1024, forms.MultipartBodyLengthLimit);
        Assert.Equal(64 * 1024, forms.ValueLengthLimit);
        Assert.Equal(256, forms.KeyLengthLimit);
        Assert.Equal(1024, forms.ValueCountLimit);
        var forwarded = factory.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            forwarded.ForwardedHeaders);
        Assert.Equal(1, forwarded.ForwardLimit);
    }

    [Fact]
    public async Task Error_and_status_pages_keep_internal_details_out_of_responses()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, "admin@compliance.demo");

        var failure = await browser.GetAsync("/test-error");
        Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        var failureHtml = await failure.Content.ReadAsStringAsync();
        Assert.Contains("Something went wrong. Please try again.", failureHtml);
        Assert.DoesNotContain("Internal diagnostic detail", failureHtml);
        Assert.DoesNotContain("stack trace", failureHtml, StringComparison.OrdinalIgnoreCase);
        AssertSecurityHeaders(failure);

        var missing = await browser.GetAsync("/a-route-that-does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var missingHtml = await missing.Content.ReadAsStringAsync();
        Assert.Contains("This page could not be found.", missingHtml);
        Assert.DoesNotContain("Exception", missingHtml);
        Assert.True(missing.Headers.CacheControl!.NoStore);

        var directError = await browser.GetAsync("/Home/Error");
        Assert.Equal(HttpStatusCode.InternalServerError, directError.StatusCode);
        var invalidStatus = await browser.GetAsync("/Home/Status/200");
        Assert.Equal(HttpStatusCode.NotFound, invalidStatus.StatusCode);
    }

    [Fact]
    public async Task Role_pages_have_landmarks_unique_controls_labels_and_table_captions()
    {
        await using var factory = new ComplianceWebFactory();
        using var browser = factory.Browser();
        await ComplianceWebFactory.LoginAsync(browser, "admin@compliance.demo");
        var workspace = await Workspace(browser);
        var ids = await Ids(factory, workspace);
        foreach (var path in new[] { "/Dashboard", "/Branches", "/Templates", "/Periods", "/Reports", "/Audit" })
            AssertAccessible(await browser.GetStringAsync(path));

        await SwitchRoleAsync(browser, "branch@compliance.demo");
        var submission = await browser.GetStringAsync($"/Submissions/Details/{ids.HarborCurrentId}");
        AssertAccessible(submission);
        Assert.Contains("Everyday Branch Readiness v1", submission);
        Assert.DoesNotContain("v@period", submission);
        await SwitchRoleAsync(browser, "assessor@compliance.demo");
        AssertAccessible(await browser.GetStringAsync($"/Scoring/Details/{ids.MapleCurrentId}"));
        await SwitchRoleAsync(browser, "approver@compliance.demo");
        AssertAccessible(await browser.GetStringAsync($"/Approvals/Details/{ids.HistoricalPeriodId}"));

        var css = await browser.GetStringAsync("/css/site.css");
        foreach (var breakpoint in new[] { "max-width: 575.98px", "max-width: 767.98px", "max-width: 991.98px", "max-width: 1199.98px" })
            Assert.Contains(breakpoint, css);
        foreach (var selector in new[] { ".response-readonly", ".score-entry", ".approval-columns", ".appeal-effect" })
            Assert.Contains(selector, css);
    }

    [Fact]
    public void Expected_application_failures_use_a_safe_structured_log_event()
    {
        var logger = new RecordingLogger<ApplicationExceptionFilter>();
        var filter = new ApplicationExceptionFilter(logger);
        var action = new ActionContext(new DefaultHttpContext(), new RouteData(),
            new ControllerActionDescriptor { DisplayName = "ReportsController.Index" }, new ModelStateDictionary());
        var context = new ExceptionContext(action, [])
        {
            Exception = new DomainRuleException("Sensitive-looking supplied value must not enter logs.")
        };
        filter.OnException(context);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(1001, entry.EventId.Id);
        Assert.Contains("status 400", entry.Message);
        Assert.Contains("DomainRuleException", entry.Message);
        Assert.DoesNotContain("Sensitive-looking supplied value", entry.Message);
        Assert.True(context.ExceptionHandled);
    }

    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("none", response.Headers.GetValues("X-Permitted-Cross-Domain-Policies").Single());
        Assert.Equal("same-origin", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("same-origin", response.Headers.GetValues("Cross-Origin-Opener-Policy").Single());
        Assert.Equal("same-origin", response.Headers.GetValues("Cross-Origin-Resource-Policy").Single());
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("style-src 'self' 'unsafe-hashes' 'sha256-aqNNdDLnnrDOnTNdkJpYlAxKVJtLt9CtFLklmInuUAE='", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("object-src 'none'", csp);
        Assert.DoesNotContain("unsafe-inline", csp);
        Assert.DoesNotContain("unsafe-eval", csp);
    }

    private static void AssertAccessible(string html)
    {
        Assert.Contains("<html lang=\"en\" data-bs-theme=\"light\">", html);
        Assert.Contains("class=\"skip-link\"", html);
        Assert.Contains("<main class=\"app-main\" id=\"main-content\" tabindex=\"-1\">", html);
        Assert.Contains("aria-label=\"Primary navigation\"", html);
        Assert.Contains("<h1", html);
        Assert.DoesNotContain(" onclick=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<style", html, StringComparison.OrdinalIgnoreCase);

        var ids = Regex.Matches(html, "\\sid=\"([^\"]+)\"", RegexOptions.IgnoreCase)
            .Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(Regex.Matches(html, "<table\\b", RegexOptions.IgnoreCase).Count,
            Regex.Matches(html, "<caption\\b", RegexOptions.IgnoreCase).Count);

        foreach (Match control in Regex.Matches(html, "<(input|select|textarea)\\b[^>]*>", RegexOptions.IgnoreCase))
        {
            if (Regex.IsMatch(control.Value, "type=\"hidden\"", RegexOptions.IgnoreCase)) continue;
            var id = Regex.Match(control.Value, "\\sid=\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (id.Success)
            {
                Assert.Contains($"for=\"{id.Groups[1].Value}\"", html, StringComparison.OrdinalIgnoreCase);
                continue;
            }
            var labelStart = html.LastIndexOf("<label", control.Index, StringComparison.OrdinalIgnoreCase);
            var labelEnd = labelStart < 0 ? -1 : html.IndexOf("</label>", labelStart, StringComparison.OrdinalIgnoreCase);
            Assert.True(labelStart >= 0 && labelEnd > control.Index,
                $"Control lacks an associated label: {control.Value}");
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, EventId EventId, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, eventId, formatter(state, exception)));
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}
