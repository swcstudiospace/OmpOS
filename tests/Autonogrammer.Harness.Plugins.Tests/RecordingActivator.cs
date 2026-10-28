using System.Collections.Concurrent;

namespace Autonogrammer.Harness.Plugins.Tests;

/// <summary>
/// An activator that records each plugin it activates. Its behaviour is supplied per test, so a test can make activation
/// throw asynchronously.
/// </summary>
internal sealed class RecordingActivator : IPluginActivator
{
    private readonly ConcurrentQueue<string> _activated = new();
    private readonly Func<PluginManifest, CancellationToken, Task> _onActivate;

    /// <summary>
    /// Creates an activator. With no behaviour, activation succeeds immediately.
    /// </summary>
    /// <param name="onActivate">Runs during activation of each plugin.</param>
    public RecordingActivator(Func<PluginManifest, CancellationToken, Task>? onActivate = null)
    {
        _onActivate = onActivate ?? ((_, _) => Task.CompletedTask);
    }

    /// <summary>
    /// The identifiers of the plugins whose activation was started, in order.
    /// </summary>
    public IReadOnlyList<string> Activated => [.. _activated];

    /// <inheritdoc />
    public async Task ActivateAsync(PluginManifest manifest, CancellationToken cancellationToken)
    {
        _activated.Enqueue(manifest.Id);
        await _onActivate(manifest, cancellationToken).ConfigureAwait(false);
    }
}
