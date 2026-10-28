using System.Collections.Immutable;
using System.Text.Json;

namespace OmpOS.Harness.AgentLoop.Hooks;

/// <summary>
/// The host-level state of hooks that is not consent itself: the global <c>Run hooks</c> switch, whether Claude settings are discovered, the
/// workspaces whose project hooks may be loaded, and the last approved version of every hook. Reads use an immutable snapshot and never wait;
/// each write reaches the disk (owner-only, atomically) before the snapshot changes, so a failed write leaves the previous state in force.
/// A file that cannot be read is treated as empty: nothing is trusted and nothing was approved, which is the safe direction.
/// </summary>
internal sealed class HooksHostStore : IDisposable
{
    private readonly string _path;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private Snapshot _snapshot;

    private HooksHostStore(string path, Snapshot snapshot)
    {
        _path = path;
        _snapshot = snapshot;
    }

    /// <summary>Gets a value indicating whether hooks run at all.</summary>
    internal bool RunHooks => _snapshot.RunHooks;

    /// <summary>Gets a value indicating whether Claude settings files are discovered.</summary>
    internal bool IncludeThirdPartyConfigs => _snapshot.IncludeThirdPartyConfigs;

    /// <summary>Opens the store at <paramref name="path"/>; a missing or unreadable file starts it empty.</summary>
    /// <param name="path">The state file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The store.</returns>
    internal static async Task<HooksHostStore> OpenAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var snapshot = Snapshot.Empty;
        if (File.Exists(fullPath))
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
                if (JsonSerializer.Deserialize(bytes, HooksHostJsonContext.Default.HooksHostDocument) is { } document)
                {
                    snapshot = Snapshot.FromDocument(document);
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                snapshot = Snapshot.Empty;
            }
        }

        return new HooksHostStore(fullPath, snapshot);
    }

    /// <inheritdoc/>
    public void Dispose() => _writeGate.Dispose();

    /// <summary>Returns whether project hooks of a workspace root may be loaded.</summary>
    /// <param name="root">The workspace root.</param>
    /// <returns>True when the user trusted the root.</returns>
    internal bool IsTrusted(string root) => _snapshot.Trusted.Contains(Normalize(root));

    /// <summary>Returns the last approved version of a hook, or null.</summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <returns>The snapshot, or null.</returns>
    internal HookApprovalSnapshot? Approval(string hookId) => _snapshot.Approvals.TryGetValue(hookId, out var approval) ? approval : null;

    /// <summary>Sets the global <c>Run hooks</c> switch.</summary>
    /// <param name="enabled">True to run hooks.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the value is stored.</returns>
    internal Task SetRunHooksAsync(bool enabled, CancellationToken cancellationToken) =>
        MutateAsync(state => state.RunHooks == enabled ? state : state with { RunHooks = enabled }, cancellationToken);

    /// <summary>Sets whether Claude settings files are discovered.</summary>
    /// <param name="enabled">True to discover them.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the value is stored.</returns>
    internal Task SetIncludeThirdPartyConfigsAsync(bool enabled, CancellationToken cancellationToken) =>
        MutateAsync(state => state.IncludeThirdPartyConfigs == enabled ? state : state with { IncludeThirdPartyConfigs = enabled }, cancellationToken);

    /// <summary>Trusts or distrusts a workspace root for loading project hooks.</summary>
    /// <param name="root">The workspace root.</param>
    /// <param name="trusted">True to trust it.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the value is stored.</returns>
    internal Task SetTrustedAsync(string root, bool trusted, CancellationToken cancellationToken)
    {
        var key = Normalize(root);
        return MutateAsync(
            state => trusted == state.Trusted.Contains(key)
                ? state
                : state with { Trusted = trusted ? state.Trusted.Add(key) : state.Trusted.Remove(key) },
            cancellationToken);
    }

    /// <summary>Records what the user approved for a hook, replacing the earlier record of the same hook.</summary>
    /// <param name="approval">The approved version.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the record is stored.</returns>
    internal Task RecordApprovalAsync(HookApprovalSnapshot approval, CancellationToken cancellationToken) =>
        MutateAsync(state => state with { Approvals = state.Approvals.SetItem(approval.HookId, approval) }, cancellationToken);

    private static string Normalize(string root)
    {
        var full = Path.GetFullPath(root);
        return Path.TrimEndingDirectorySeparator(full);
    }

    private async Task MutateAsync(Func<Snapshot, Snapshot> change, CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = _snapshot;
            var next = change(current);
            if (ReferenceEquals(next, current))
            {
                return;
            }

            await WriteAsync(next, cancellationToken).ConfigureAwait(false);
            _snapshot = next;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task WriteAsync(Snapshot snapshot, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(directory);
            }
            else
            {
                Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot.ToDocument(), HooksHostJsonContext.Default.HooksHostDocument);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 4096,
                Options = FileOptions.Asynchronous,
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            var stream = new FileStream(temporary, options);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private sealed record Snapshot(
        bool RunHooks,
        bool IncludeThirdPartyConfigs,
        ImmutableHashSet<string> Trusted,
        ImmutableDictionary<string, HookApprovalSnapshot> Approvals)
    {
        public static Snapshot Empty { get; } = new(
            true,
            true,
            ImmutableHashSet.Create<string>(StringComparer.Ordinal),
            ImmutableDictionary.Create<string, HookApprovalSnapshot>(StringComparer.Ordinal));

        public static Snapshot FromDocument(HooksHostDocument document) => new(
            document.RunHooks,
            document.IncludeThirdPartyConfigs,
            document.TrustedWorkspaces.ToImmutableHashSet(StringComparer.Ordinal),
            document.Approvals
                .GroupBy(approval => approval.HookId, StringComparer.Ordinal)
                .ToImmutableDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal));

        public HooksHostDocument ToDocument() => new()
        {
            RunHooks = RunHooks,
            IncludeThirdPartyConfigs = IncludeThirdPartyConfigs,
            TrustedWorkspaces = [.. Trusted.Order(StringComparer.Ordinal)],
            Approvals = [.. Approvals.Values.OrderBy(approval => approval.HookId, StringComparer.Ordinal)],
        };
    }
}
