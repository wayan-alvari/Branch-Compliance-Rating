using System.Globalization;
using System.Security.Cryptography;
using BranchCompliance.Application.Security;
using BranchCompliance.Application.Submissions;
using BranchCompliance.Domain.Rules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BranchCompliance.Infrastructure.Workspaces;

public sealed class WorkspaceFileStore : IEvidenceStorage
{
    private const int MaximumEvidenceLength = 8 * 1024 * 1024;
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

    public async Task<InspectedEvidence> InspectAsync(Stream content, string originalName, string claimedMediaType,
        long claimedLength, CancellationToken cancellationToken)
    {
        Rule.Require(claimedLength is > 0 and <= MaximumEvidenceLength, "Evidence must be between 1 byte and 8 MB.");
        var mediaType = claimedMediaType.Trim().ToLowerInvariant();
        Rule.Require(mediaType is "application/pdf" or "image/jpeg" or "image/png",
            "Only PDF, JPEG, and PNG evidence is allowed.");
        var safeName = SafeFileName(originalName);
        var extension = Path.GetExtension(safeName).ToLowerInvariant();
        Rule.Require(mediaType switch
        {
            "application/pdf" => extension == ".pdf",
            "image/jpeg" => extension is ".jpg" or ".jpeg",
            "image/png" => extension == ".png",
            _ => false
        }, "The file extension must match the claimed evidence type.");

        using var collected = new MemoryStream((int)claimedLength);
        var buffer = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            Rule.Require(collected.Length + read <= MaximumEvidenceLength, "Evidence must be between 1 byte and 8 MB.");
            await collected.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        var bytes = collected.ToArray();
        Rule.Require(bytes.LongLength == claimedLength, "The uploaded evidence length did not match the request.");
        Rule.Require(HasExpectedMagic(bytes, mediaType), "The evidence content does not match its file type.");
        return new InspectedEvidence(safeName, mediaType, bytes.LongLength,
            Convert.ToHexString(SHA256.HashData(bytes)), bytes);
    }

    public async Task StoreAsync(Guid workspaceId, string storageName, ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var directory = Directory.CreateDirectory(WorkspaceDirectory(workspaceId));
        if (directory.LinkTarget is not null) throw new IOException("Linked entries are not allowed in evidence storage.");
        var path = EvidencePath(directory.FullName, storageName);
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.WriteThrough);
            await output.WriteAsync(content, cancellationToken);
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    public async Task<Stream> OpenVerifiedAsync(Guid workspaceId, string storageName, long expectedLength,
        string expectedSha256, CancellationToken cancellationToken)
    {
        var directory = new DirectoryInfo(WorkspaceDirectory(workspaceId));
        if (!directory.Exists) throw new ResourceNotFoundException();
        if (directory.LinkTarget is not null) throw new IOException("Linked entries are not allowed in evidence storage.");
        var file = new FileInfo(EvidencePath(directory.FullName, storageName));
        if (!file.Exists) throw new ResourceNotFoundException();
        if (file.LinkTarget is not null) throw new IOException("Linked entries are not allowed in evidence storage.");
        var bytes = await File.ReadAllBytesAsync(file.FullName, cancellationToken);
        if (bytes.LongLength != expectedLength ||
            !Convert.ToHexString(SHA256.HashData(bytes)).Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Stored evidence failed its integrity check.");
        return new MemoryStream(bytes, writable: false);
    }

    public Task DeleteAsync(Guid workspaceId, string storageName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = new DirectoryInfo(WorkspaceDirectory(workspaceId));
        if (!directory.Exists) return Task.CompletedTask;
        if (directory.LinkTarget is not null) throw new IOException("Linked entries are not allowed in evidence storage.");
        var file = new FileInfo(EvidencePath(directory.FullName, storageName));
        if (!file.Exists) return Task.CompletedTask;
        if (file.LinkTarget is not null) throw new IOException("Linked entries are not allowed in evidence storage.");
        file.Delete();
        return Task.CompletedTask;
    }

    private static string SafeFileName(string originalName)
    {
        var leaf = originalName.Replace('\\', '/').Split('/').LastOrDefault()?.Trim() ?? "";
        var safe = new string(leaf.Select(character => char.IsAsciiLetterOrDigit(character) ||
            character is ' ' or '.' or '-' or '_' or '(' or ')' ? character : '_').ToArray()).Trim(' ', '.');
        var extension = Path.GetExtension(safe);
        var stem = Path.GetFileNameWithoutExtension(safe).Trim();
        if (stem.Length == 0) stem = "evidence";
        if (stem.Length + extension.Length > 150) stem = stem[..(150 - extension.Length)];
        return stem + extension;
    }

    private static bool HasExpectedMagic(ReadOnlySpan<byte> content, string mediaType) => mediaType switch
    {
        "application/pdf" => content.StartsWith("%PDF-"u8),
        "image/png" => content.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        "image/jpeg" => content.Length >= 5 && content[0] == 0xFF && content[1] == 0xD8 &&
                        content[2] == 0xFF && content[^2] == 0xFF && content[^1] == 0xD9,
        _ => false
    };

    private static string EvidencePath(string directory, string storageName)
    {
        var extension = Path.GetExtension(storageName).ToLowerInvariant();
        var stem = Path.GetFileNameWithoutExtension(storageName);
        if (!Guid.TryParseExact(stem, "N", out _) || extension is not (".pdf" or ".png" or ".jpg"))
            throw new InvalidDataException("Invalid evidence storage name.");
        var path = Path.GetFullPath(Path.Combine(directory, storageName));
        if (!path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid evidence storage path.");
        return path;
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
