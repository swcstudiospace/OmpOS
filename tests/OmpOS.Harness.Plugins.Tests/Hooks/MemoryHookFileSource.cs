using System.Security.Cryptography;
using System.Text;
using OmpOS.Harness.Plugins.Hooks;

namespace OmpOS.Harness.Plugins.Tests.Hooks;

/// <summary>
/// An in-memory file system for the hook loader: paths map to bytes, and a test marks which files are tamper resistant.
/// </summary>
internal sealed class MemoryHookFileSource : IHookFileSource
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _tamperResistant = new(StringComparer.Ordinal);

    /// <summary>Adds or replaces a file.</summary>
    /// <param name="path">The absolute path.</param>
    /// <param name="content">The UTF-8 text of the file.</param>
    /// <param name="tamperResistant">True to report the file as not writable by group or others.</param>
    public MemoryHookFileSource With(string path, string content, bool tamperResistant = false)
    {
        _files[path] = Encoding.UTF8.GetBytes(content);
        if (tamperResistant)
        {
            _tamperResistant.Add(path);
        }

        return this;
    }

    /// <inheritdoc />
    public HookFileInfo Inspect(string path) =>
        _files.TryGetValue(path, out var bytes)
            ? new HookFileInfo(true, bytes.Length, _tamperResistant.Contains(path))
            : new HookFileInfo(false, 0, false);

    /// <inheritdoc />
    public ValueTask<byte[]?> ReadAsync(string path, int maxBytes, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_files.TryGetValue(path, out var bytes) && bytes.Length <= maxBytes ? bytes : null);

    /// <inheritdoc />
    public ValueTask<string?> DigestAsync(string path, int maxBytes, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            _files.TryGetValue(path, out var bytes) && bytes.Length <= maxBytes
                ? Convert.ToHexStringLower(SHA256.HashData(bytes))
                : null);
}
