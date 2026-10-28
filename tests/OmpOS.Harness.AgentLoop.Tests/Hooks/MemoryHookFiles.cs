using System.Security.Cryptography;
using System.Text;
using OmpOS.Harness.Plugins.Hooks;

namespace OmpOS.Harness.AgentLoop.Tests.Hooks;

/// <summary>An in-memory file system for the hook loader: paths map to bytes. A test replaces a file to simulate an edit.</summary>
internal sealed class MemoryHookFiles : IHookFileSource
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>Adds or replaces a file.</summary>
    /// <param name="path">The absolute path.</param>
    /// <param name="content">The UTF-8 text.</param>
    public void Set(string path, string content)
    {
        lock (_gate)
        {
            _files[path] = Encoding.UTF8.GetBytes(content);
        }
    }

    /// <inheritdoc />
    public HookFileInfo Inspect(string path)
    {
        lock (_gate)
        {
            return _files.TryGetValue(path, out var bytes) ? new HookFileInfo(true, bytes.Length, false) : new HookFileInfo(false, 0, false);
        }
    }

    /// <inheritdoc />
    public ValueTask<byte[]?> ReadAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(_files.TryGetValue(path, out var bytes) && bytes.Length <= maxBytes ? bytes : null);
        }
    }

    /// <inheritdoc />
    public ValueTask<string?> DigestAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(
                _files.TryGetValue(path, out var bytes) && bytes.Length <= maxBytes ? Convert.ToHexStringLower(SHA256.HashData(bytes)) : null);
        }
    }
}
