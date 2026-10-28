using OmpOS.Harness.Plugins.Hooks;

namespace OmpOS.Native.Composition;

/// <summary>
/// The hook file source of a session whose workspace is on an ssh host: it finds no hook file. The hooks service reads files from this machine, and the project and user files of the
/// remote host are not read in this version, so only the hooks of plugins and the hook events the ACP session itself reports apply. The session says so in its stages.
/// </summary>
internal sealed class NoHookFiles : IHookFileSource
{
    /// <inheritdoc/>
    public HookFileInfo Inspect(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new HookFileInfo(false, 0, false);
    }

    /// <inheritdoc/>
    public ValueTask<byte[]?> ReadAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return ValueTask.FromResult<byte[]?>(null);
    }

    /// <inheritdoc/>
    public ValueTask<string?> DigestAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return ValueTask.FromResult<string?>(null);
    }
}
