using BranchCompliance.Application.Workspaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BranchCompliance.Infrastructure.Workspaces;

public sealed class WorkspaceCleanupService(IServiceScopeFactory scopes, WorkspaceCoordinator coordinator,
    IConfiguration configuration, ILogger<WorkspaceCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("DemoMode:Enabled")) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var lease = await coordinator.EnterAsync(stoppingToken);
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<IWorkspaceLifecycle>().CleanupAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception)
                {
                    // Do not log exception messages that might contain local file paths or connection details.
                    logger.LogWarning("Workspace cleanup did not complete; it will retry on the next interval.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
