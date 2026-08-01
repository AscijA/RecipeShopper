using System.Security.Cryptography;

namespace RecipeShopper.Infrastructure.Files;

public interface IImageAssetStore
{
    Task<byte[]?> ReadAsync(string relativePath, CancellationToken cancellationToken = default);
    Task WriteAsync(string relativePath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}

public sealed class AppImageStore : IImageAssetStore
{
    private readonly string _rootDirectory;

    public AppImageStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _rootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(_rootDirectory);
    }

    public async Task<byte[]?> ReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var path = Resolve(relativePath);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false) : null;
    }

    public async Task WriteAsync(string relativePath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        var destination = Resolve(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, content, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, destination, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(relativePath);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public static string Sha256(ReadOnlySpan<byte> content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private string Resolve(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathRooted(relativePath)) throw new ArgumentException("Image paths must be relative.", nameof(relativePath));

        var resolved = Path.GetFullPath(Path.Combine(_rootDirectory, relativePath));
        var prefix = _rootDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? _rootDirectory
            : _rootDirectory + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Image path escapes app-private storage.", nameof(relativePath));
        }

        return resolved;
    }
}
