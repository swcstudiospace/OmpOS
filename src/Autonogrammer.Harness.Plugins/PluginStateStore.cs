using System.Collections.Immutable;
using System.Text.Json;

namespace Autonogrammer.Harness.Plugins;

/// <summary>
/// The host's persisted state: quarantines with their reasons, consent for command hashes, and hook order and
/// enablement. Reads use an immutable snapshot and never wait. Writes are serialized, and each one reaches the disk
/// before the snapshot changes, so a failed write leaves the previous state in force.
/// </summary>
public sealed class PluginStateStore : IDisposable
{
    private readonly string _path;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private PluginStateSnapshot _snapshot;

    private PluginStateStore(string path, TimeProvider clock, PluginStateSnapshot snapshot)
    {
        _path = path;
        _clock = clock;
        _snapshot = snapshot;
    }

    /// <summary>
    /// Opens the state file at <paramref name="path"/>, or starts empty when the file does not exist.
    /// </summary>
    /// <param name="path">The state file. Its directory is created on the first write.</param>
    /// <param name="clock">The clock that stamps quarantines. The system clock when null.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The opened store.</returns>
    /// <exception cref="JsonException">
    /// The state file exists but cannot be read. The store refuses to open, so no hook is assumed consented.
    /// </exception>
    public static async Task<PluginStateStore> OpenAsync(string path, TimeProvider? clock, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var snapshot = PluginStateSnapshot.Empty;
        if (File.Exists(fullPath))
        {
            var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
            var document = JsonSerializer.Deserialize(bytes, PluginJsonContext.Default.PluginStateDocument)
                ?? throw new JsonException("The plugin state file is empty.");
            snapshot = PluginStateSnapshot.FromDocument(document);
        }

        return new PluginStateStore(fullPath, clock ?? TimeProvider.System, snapshot);
    }

    /// <summary>
    /// Releases the lock that serializes writes. The state on disk is already current after every change.
    /// </summary>
    public void Dispose() => _writeGate.Dispose();

    /// <summary>
    /// Returns the quarantine recorded for a plugin, or null when the plugin is not quarantined.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    public PluginQuarantine? FindQuarantine(string pluginId) =>
        _snapshot.Quarantines.TryGetValue(pluginId, out var quarantine) ? quarantine : null;

    /// <summary>
    /// Returns true when consent is recorded for a command hash.
    /// </summary>
    /// <param name="commandHash">The hash from <see cref="HookCommandIdentity.ComputeHash"/>.</param>
    public bool IsConsented(string commandHash) => _snapshot.ConsentedHashes.Contains(commandHash);

    /// <summary>
    /// Returns true when the hook is disabled.
    /// </summary>
    /// <param name="hookId">The hook identifier.</param>
    public bool IsHookDisabled(string hookId) => _snapshot.DisabledHooks.Contains(hookId);

    /// <summary>
    /// The hook identifiers in the order the user chose, earliest first.
    /// </summary>
    public ImmutableList<string> HookOrder => _snapshot.HookOrder;

    /// <summary>
    /// Every quarantine that an operator lifted, oldest first, with the reason given for each.
    /// </summary>
    public ImmutableList<PluginLift> Lifts => _snapshot.Lifts;

    /// <summary>
    /// Quarantines a plugin with a reason and persists it. A plugin that is already quarantined keeps its first reason.
    /// </summary>
    /// <param name="pluginId">The plugin to quarantine.</param>
    /// <param name="reason">Why it was quarantined.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task QuarantineAsync(string pluginId, string reason, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        await MutateAsync(
            state => state.Quarantines.ContainsKey(pluginId)
                ? state
                : state with
                {
                    Quarantines = state.Quarantines.Add(pluginId, new PluginQuarantine(pluginId, reason, _clock.GetUtcNow())),
                },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lifts a plugin's quarantine, records the operator's reason, and persists both. A plugin that is not quarantined is
    /// left unchanged.
    /// </summary>
    /// <param name="pluginId">The plugin whose quarantine is lifted.</param>
    /// <param name="reason">Why the operator lifted the quarantine.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when a quarantine was lifted; false when the plugin was not quarantined.</returns>
    public async Task<bool> LiftQuarantineAsync(string pluginId, string reason, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var lifted = false;
        await MutateAsync(
            state =>
            {
                if (!state.Quarantines.ContainsKey(pluginId))
                {
                    return state;
                }

                lifted = true;
                return state with
                {
                    Quarantines = state.Quarantines.Remove(pluginId),
                    Lifts = state.Lifts.Add(new PluginLift(pluginId, reason, _clock.GetUtcNow())),
                };
            },
            cancellationToken).ConfigureAwait(false);
        return lifted;
    }

    /// <summary>
    /// Records consent for a command hash and persists it.
    /// </summary>
    /// <param name="commandHash">The hash of the exact command the user approved.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task RecordConsentAsync(string commandHash, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandHash);
        await MutateAsync(
            state => state with { ConsentedHashes = state.ConsentedHashes.Add(commandHash) },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Withdraws the consent for a command hash and persists it. A hash without consent is left unchanged.
    /// </summary>
    /// <param name="commandHash">The hash whose consent is withdrawn.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task RevokeConsentAsync(string commandHash, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandHash);
        await MutateAsync(
            state => state.ConsentedHashes.Contains(commandHash)
                ? state with { ConsentedHashes = state.ConsentedHashes.Remove(commandHash) }
                : state,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets whether a hook is disabled and persists it.
    /// </summary>
    /// <param name="hookId">The hook identifier.</param>
    /// <param name="disabled">True to disable the hook; false to enable it.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task SetHookDisabledAsync(string hookId, bool disabled, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hookId);
        await MutateAsync(
            state => state with
            {
                DisabledHooks = disabled ? state.DisabledHooks.Add(hookId) : state.DisabledHooks.Remove(hookId),
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Persists the hook order.
    /// </summary>
    /// <param name="order">Every hook identifier, in run order.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task SetHookOrderAsync(IReadOnlyList<string> order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        await MutateAsync(
            state => state with { HookOrder = order.ToImmutableList() },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task MutateAsync(Func<PluginStateSnapshot, PluginStateSnapshot> change, CancellationToken cancellationToken)
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

    private async Task WriteAsync(PluginStateSnapshot snapshot, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot.ToDocument(), PluginJsonContext.Default.PluginStateDocument);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
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

    private sealed record PluginStateSnapshot(
        ImmutableDictionary<string, PluginQuarantine> Quarantines,
        ImmutableList<PluginLift> Lifts,
        ImmutableHashSet<string> ConsentedHashes,
        ImmutableList<string> HookOrder,
        ImmutableHashSet<string> DisabledHooks)
    {
        public static PluginStateSnapshot Empty { get; } = new(
            ImmutableDictionary.Create<string, PluginQuarantine>(StringComparer.Ordinal),
            ImmutableList<PluginLift>.Empty,
            ImmutableHashSet.Create<string>(StringComparer.Ordinal),
            ImmutableList<string>.Empty,
            ImmutableHashSet.Create<string>(StringComparer.Ordinal));

        public static PluginStateSnapshot FromDocument(PluginStateDocument document) => new(
            document.Quarantines.ToImmutableDictionary(
                quarantine => quarantine.PluginId,
                quarantine => new PluginQuarantine(quarantine.PluginId, quarantine.Reason, quarantine.QuarantinedAt),
                StringComparer.Ordinal),
            document.Lifts.Select(lift => new PluginLift(lift.PluginId, lift.Reason, lift.LiftedAt)).ToImmutableList(),
            document.ConsentedHashes.ToImmutableHashSet(StringComparer.Ordinal),
            document.HookOrder.ToImmutableList(),
            document.DisabledHooks.ToImmutableHashSet(StringComparer.Ordinal));

        public PluginStateDocument ToDocument() => new()
        {
            Quarantines = [.. Quarantines.Values.Select(quarantine => new QuarantineDocument
            {
                PluginId = quarantine.PluginId,
                Reason = quarantine.Reason,
                QuarantinedAt = quarantine.QuarantinedAt,
            })],
            Lifts = [.. Lifts.Select(lift => new LiftDocument
            {
                PluginId = lift.PluginId,
                Reason = lift.Reason,
                LiftedAt = lift.LiftedAt,
            })],
            ConsentedHashes = [.. ConsentedHashes],
            HookOrder = [.. HookOrder],
            DisabledHooks = [.. DisabledHooks],
        };
    }
}
