using System.Globalization;
using System.Threading.Channels;

namespace Autonogrammer.Presentation.Dispatch;

/// <summary>
/// An in-memory host for tests and for heads that have no hub yet. It keeps the devices, commands and capability policy it was given, records
/// every rename, revocation, send, cancel, capability change and pause, and carries the feed changes a test publishes. It makes no network call.
/// </summary>
public sealed class FakeDispatchHost : IDispatchHost, IThisDeviceHost
{
    private readonly List<DispatchDevice> _devices;
    private readonly List<DispatchCommand> _commands = [];
    private readonly Dictionary<string, bool> _policy = new(StringComparer.Ordinal);
    private readonly List<(string DeviceId, string Name)> _renames = [];
    private readonly List<string> _revoked = [];
    private readonly List<(string DeviceId, string Operation, string Text)> _sent = [];
    private readonly List<string> _cancelled = [];
    private readonly List<(string Capability, bool Enabled)> _capabilityChanges = [];
    private readonly List<bool> _pauseChanges = [];
    private readonly Channel<DispatchFeedChange> _feed = Channel.CreateUnbounded<DispatchFeedChange>();
    private bool _paused;

    /// <summary>Creates a fake that lists the given devices, commands and capability policy.</summary>
    /// <param name="devices">The devices the fake lists.</param>
    /// <param name="commands">The commands the fake lists, newest first; none when not given.</param>
    /// <param name="capabilities">The per-capability policy; when not given, sessions is allowed and code and cowork are not.</param>
    public FakeDispatchHost(
        IReadOnlyList<DispatchDevice> devices,
        IReadOnlyList<DispatchCommand>? commands = null,
        IReadOnlyList<DispatchCapabilityPolicy>? capabilities = null)
    {
        _devices = [.. devices];
        if (commands is not null)
        {
            _commands.AddRange(commands);
        }

        foreach (var policy in capabilities ?? DefaultCapabilities())
        {
            _policy[policy.Capability] = policy.Enabled;
        }
    }

    /// <summary>Gets the renames received, in order.</summary>
    public IReadOnlyList<(string DeviceId, string Name)> Renames => _renames;

    /// <summary>Gets the devices revoked, in order.</summary>
    public IReadOnlyList<string> Revoked => _revoked;

    /// <summary>Gets the commands sent, in order.</summary>
    public IReadOnlyList<(string DeviceId, string Operation, string Text)> Sent => _sent;

    /// <summary>Gets the commands cancelled, in order.</summary>
    public IReadOnlyList<string> Cancelled => _cancelled;

    /// <summary>Gets the capability changes received, in order.</summary>
    public IReadOnlyList<(string Capability, bool Enabled)> CapabilityChanges => _capabilityChanges;

    /// <summary>Gets the pause changes received, in order: true pauses Dispatch and false resumes it.</summary>
    public IReadOnlyList<bool> PauseChanges => _pauseChanges;

    /// <inheritdoc/>
    public Task<IReadOnlyList<DispatchDevice>> ListDevicesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DispatchDevice>>(_devices.ToArray());

    /// <inheritdoc/>
    public Task<IReadOnlyList<DispatchCommand>> ListCommandsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DispatchCommand>>(_commands.ToArray());

    /// <inheritdoc/>
    public Task RenameDeviceAsync(string deviceId, string name, CancellationToken cancellationToken)
    {
        _renames.Add((deviceId, name));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RevokeDeviceAsync(string deviceId, CancellationToken cancellationToken)
    {
        _revoked.Add(deviceId);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RevokeAllDevicesAsync(CancellationToken cancellationToken)
    {
        _revoked.AddRange(_devices.Select(device => device.Id));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<string> SendAsync(string deviceId, string operation, string text, CancellationToken cancellationToken)
    {
        _sent.Add((deviceId, operation, text));
        return Task.FromResult("command-" + _sent.Count.ToString(CultureInfo.InvariantCulture));
    }

    /// <inheritdoc/>
    public Task CancelAsync(string commandId, CancellationToken cancellationToken)
    {
        _cancelled.Add(commandId);
        var index = _commands.FindIndex(command => string.Equals(command.CommandId, commandId, StringComparison.Ordinal));
        if (index >= 0)
        {
            _commands[index] = _commands[index] with { Phase = DispatchPhase.Cancelled };
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<DispatchFeedChange> WatchFeedAsync(CancellationToken cancellationToken) =>
        _feed.Reader.ReadAllAsync(cancellationToken);

    /// <summary>Publishes one feed change, as the hub would.</summary>
    /// <param name="change">The change.</param>
    public void Publish(DispatchFeedChange change) => _feed.Writer.TryWrite(change);

    /// <inheritdoc/>
    public Task<DispatchThisDevice> GetThisDeviceAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new DispatchThisDevice(
            Paired: true,
            Paused: _paused,
            Capabilities: _policy.Select(entry => new DispatchCapabilityPolicy(entry.Key, entry.Value)).ToArray(),
            PendingApprovals: []));

    /// <inheritdoc/>
    public Task SetCapabilityAsync(string capability, bool enabled, CancellationToken cancellationToken)
    {
        _policy[capability] = enabled;
        _capabilityChanges.Add((capability, enabled));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SetPausedAsync(bool paused, CancellationToken cancellationToken)
    {
        _paused = paused;
        _pauseChanges.Add(paused);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ResolveApprovalAsync(string approvalId, bool approved, CancellationToken cancellationToken) => Task.CompletedTask;

    private static IReadOnlyList<DispatchCapabilityPolicy> DefaultCapabilities() =>
        [new DispatchCapabilityPolicy("sessions", true), new DispatchCapabilityPolicy("code", false), new DispatchCapabilityPolicy("cowork", false)];
}
