using System.Net;
using System.Text.RegularExpressions;
using BranchCompliance.Application.Workspaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BranchCompliance.IntegrationTests;

public sealed class ComplianceWebFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _keepAlive;
    private readonly string _testRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.local/test-runs"));
    public string FilesRoot { get; }
    public FakeClock Clock { get; } = new();

    public ComplianceWebFactory()
    {
        FilesRoot = Path.Combine(_testRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(FilesRoot);
        _keepAlive = new SqliteConnection($"Data Source=auth-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        _keepAlive.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DemoMode:Enabled"] = "true",
            ["Database:Initialize"] = "true",
            ["Database:Provider"] = "Sqlite",
            ["ConnectionStrings:DemoSqlite"] = _keepAlive.ConnectionString,
            ["DataProtection:KeyPath"] = Path.Combine(FilesRoot, "keys"),
            ["Evidence:RootPath"] = Path.Combine(FilesRoot, "evidence")
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
            services.AddControllers().AddApplicationPart(typeof(PolicyProbeController).Assembly);
        });
    }

    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
        BaseAddress = new Uri("https://localhost")
    });

    public static async Task<string> TokenAsync(HttpClient browser, string path)
    {
        var response = await browser.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var input = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>").Value;
        var token = Regex.Match(input, "value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        return WebUtility.HtmlDecode(token);
    }

    public static async Task<HttpResponseMessage> LoginAsync(HttpClient browser, string email, string password = "PortfolioDemo123!", string? returnUrl = null)
    {
        var token = await TokenAsync(browser, "/Account/Login");
        return await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["ReturnUrl"] = returnUrl ?? "",
            ["__RequestVerificationToken"] = token
        }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAlive.Dispose();
            var resolved = Path.GetFullPath(FilesRoot);
            if (resolved.StartsWith(_testRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolved))
            {
                Directory.Delete(resolved, recursive: true);
            }
        }
    }
}
