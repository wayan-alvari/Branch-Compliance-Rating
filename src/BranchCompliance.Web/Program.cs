using System.Globalization;
using System.Threading.RateLimiting;
using BranchCompliance.Application.Security;
using BranchCompliance.Infrastructure;
using BranchCompliance.Infrastructure.Identity;
using BranchCompliance.Infrastructure.Persistence;
using BranchCompliance.Web.Filters;
using BranchCompliance.Web.Middleware;
using BranchCompliance.Web.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
});
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddComplianceInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, HttpCurrentActor>();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var english = new CultureInfo("en-US");
    options.DefaultRequestCulture = new RequestCulture(english);
    options.SupportedCultures = [english];
    options.SupportedUICultures = [english];
});
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<ApplicationExceptionFilter>();
});
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "BranchCompliance.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 9 * 1024 * 1024;
    options.ValueLengthLimit = 64 * 1024;
    options.KeyLengthLimit = 256;
    options.ValueCountLimit = 1024;
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});
var keyPath = builder.Configuration["DataProtection:KeyPath"];
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(keyPath))
{
    throw new InvalidOperationException("Set DataProtection:KeyPath to a persistent protected directory.");
}
keyPath ??= Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../data/keys"));
builder.Services.AddDataProtection().SetApplicationName("BranchComplianceRating")
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath));

var app = builder.Build();
if (app.Configuration.GetValue<bool>("Database:Initialize"))
{
    if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue<bool>("DemoMode:Enabled"))
    {
        throw new InvalidOperationException("Initialization requires explicit Development and DemoMode settings.");
    }
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();
    if (db.Database.IsSqlite())
    {
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        await db.Database.MigrateAsync();
    }
    await scope.ServiceProvider.GetRequiredService<DemoIdentitySeeder>().SeedAsync(CancellationToken.None);
}

app.UseForwardedHeaders();
app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
    context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-hashes' 'sha256-aqNNdDLnnrDOnTNdkJpYlAxKVJtLt9CtFLklmInuUAE='; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});
app.UseStaticFiles();
app.UseRequestLocalization();
app.UseRouting();
app.Use(async (context, next) =>
{
    if (context.GetEndpoint() is not null)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers.Pragma = "no-cache";
    }
    await next();
});
app.UseAuthentication();
app.UseMiddleware<DemoWorkspaceMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();
app.MapGet("/favicon.ico", () => Results.Redirect("/favicon.svg", permanent: true)).AllowAnonymous();
app.MapGet("/health", () => Results.Text("Healthy", "text/plain")).AllowAnonymous();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();

public partial class Program;
