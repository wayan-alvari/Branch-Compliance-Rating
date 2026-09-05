using BranchCompliance.Application.Security;
using BranchCompliance.Application.Workspaces;
using BranchCompliance.Infrastructure.Identity;
using BranchCompliance.Infrastructure.Persistence;
using BranchCompliance.Infrastructure.Workspaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BranchCompliance.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddComplianceInfrastructure(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<WorkspaceContext>();
        services.AddScoped<IWorkspaceContext>(provider => provider.GetRequiredService<WorkspaceContext>());
        services.AddSingleton<WorkspaceCoordinator>();
        services.AddSingleton<WorkspaceFileStore>();
        services.AddScoped<IWorkspaceSeeder, WorkspaceSeeder>();
        services.AddScoped<IWorkspaceLifecycle, WorkspaceLifecycle>();
        services.AddHostedService<WorkspaceCleanupService>();
        services.AddDbContext<ComplianceDbContext>(options =>
        {
            if (configuration["Database:Provider"] == "Sqlite")
            {
                if (!environment.IsDevelopment())
                {
                    throw new InvalidOperationException("SQLite is available only in explicit development mode.");
                }
                var connection = configuration.GetConnectionString("DemoSqlite")
                    ?? throw new InvalidOperationException("Configure the development SQLite connection outside Git.");
                options.UseSqlite(connection);
            }
            else
            {
                var connection = configuration.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("Configure the MySQL connection using user secrets or environment variables.");
                options.UseMySql(connection, new MySqlServerVersion(new Version(8, 0, 46)));
            }
        });

        services.AddIdentity<DemoUser, IdentityRole>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireNonAlphanumeric = true;
        }).AddEntityFrameworkStores<ComplianceDbContext>();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "BranchCompliance.Identity";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
                : Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.ExpireTimeSpan = TimeSpan.FromHours(2);
            options.SlidingExpiration = true;
        });
        services.AddAuthorization(options =>
        {
            foreach (var role in DemoRoles.All)
            {
                options.AddPolicy(role, policy => policy.RequireAuthenticatedUser().RequireRole(role));
            }
        });
        services.AddScoped<DemoIdentitySeeder>();
        return services;
    }
}
