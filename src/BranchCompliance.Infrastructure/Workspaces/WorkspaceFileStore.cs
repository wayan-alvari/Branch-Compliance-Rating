using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BranchCompliance.Infrastructure.Workspaces;

public sealed class WorkspaceFileStore
{
    private readonly string _root;

    public WorkspaceFileStore(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["Evidence:RootPath"];
        if (!environment.IsDevelopment() && string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException("Configure an evidence directory outside public static content.");
        _root = Path.GetFullPath(configured ?? Path.Combine(environment.ContentRootPath, "../../evidence"));
        var publicRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot"));
        if (_root.Equals(publicRoot, StringComparison.OrdinalIgnoreCase) || _root.StartsWith(publicRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Evidence cannot be stored in public static content.");
    }

    public string WorkspaceDirectory(Guid workspaceId)
    {
        if (workspaceId == Guid.Empty) throw new ArgumentException("A workspace is required.");
        var result = Path.GetFullPath(Path.Combine(_root, workspaceId.ToString("N")));
        if (!result.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid storage location.");
        return result;
    }

    public void RemoveWorkspace(Guid workspaceId)
    {
        var directory = new DirectoryInfo(WorkspaceDirectory(workspaceId));
        if (!directory.Exists) return;
        RejectLinks(directory);
        directory.Delete(recursive: true);
    }

    public async Task RecordActivityAsync(Guid workspaceId, DateTime now, CancellationToken cancellationToken)
    {
        var directory = Directory.CreateDirectory(WorkspaceDirectory(workspaceId));
        if (directory.LinkTarget is not null) throw new IOException("Linked entries are not allowed in evidence storage.");
        var marker = Path.Combine(directory.FullName, ".activity-utc");
        var temporary = Path.Combine(directory.FullName, $".activity-{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, now.ToString("O", CultureInfo.InvariantCulture), cancellationToken);
            File.Move(temporary, marker, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public async Task<DateTime?> ReadActivityAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var directory = new DirectoryInfo(WorkspaceDirectory(workspaceId));
        if (directory.LinkTarget is not null) throw new IOException("Linked entries are not allowed in evidence storage.");
        var marker = new FileInfo(Path.Combine(directory.FullName, ".activity-utc"));
        if (!marker.Exists) return null;
        if (marker.LinkTarget is not null || marker.Length > 64) throw new IOException("Invalid activity metadata.");
        var value = await File.ReadAllTextAsync(marker.FullName, cancellationToken);
        if (!DateTime.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var observed) || observed.Kind != DateTimeKind.Utc)
            throw new IOException("Invalid activity metadata.");
        return observed;
    }

    private static void RejectLinks(DirectoryInfo directory)
    {
        if (directory.LinkTarget is not null) throw new IOException("Linked entries are not allowed in evidence storage.");
        foreach (var item in directory.EnumerateFileSystemInfos())
        {
            if (item.LinkTarget is not null) throw new IOException("Linked entries are not allowed in evidence storage.");
            if (item is DirectoryInfo child) RejectLinks(child);
        }
    }
}
