using System.Collections.Concurrent;

namespace BranchCompliance.Infrastructure.Workspaces;

// The demo is hosted by one application process. A lease spans the entire
// request so cleanup cannot delete records or streams that a request is using.
public sealed class WorkspaceCoordinator : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<Guid, DateTime> _observed = new();

    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        return new Lease(_gate);
    }

    public DateTime? LastObserved(Guid id) => _observed.TryGetValue(id, out var time) ? time : null;
    public void Observe(Guid id, DateTime now) => _observed[id] = now;
    public void Forget(Guid id) => _observed.TryRemove(id, out _);
    public void Dispose() => _gate.Dispose();

    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            gate.Release();
        }
    }
}
