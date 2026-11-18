using System.Runtime.CompilerServices;
using Autonogrammer.Presentation.Dispatch;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Dispatch;

/// <summary>The relay's page loads: an older load that finishes late never replaces the page of a newer load.</summary>
public sealed class DispatchRelayModelTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Two overlapping loads finish in the order they started, so the page shows the devices the newer load read.</summary>
    [Fact]
    public async Task OverlappingLoadsEndWithTheDevicesOfTheNewerLoad()
    {
        var host = new SlowFirstReadHost();
        var relay = new DispatchRelayModel(host, null);

        var older = relay.LoadAsync(Token);
        var newer = relay.LoadAsync(Token);
        host.ReleaseFirstRead();
        await older.ConfigureAwait(true);
        await newer.ConfigureAwait(true);

        relay.Page.Devices.Rows.Select(row => row.Name).ShouldBe(["Office Mac"]);
    }

    /// <summary>A load that fails with an unexpected error does not hold up the load queued after it.</summary>
    [Fact]
    public async Task ALoadThatFailsDoesNotHoldUpTheLoadQueuedAfterIt()
    {
        var host = new SlowFirstReadHost(new InvalidOperationException("the device store is unreadable"));
        var relay = new DispatchRelayModel(host, null);

        var failed = Should.ThrowAsync<InvalidOperationException>(async () => await relay.LoadAsync(Token).ConfigureAwait(true));
        var next = relay.LoadAsync(Token);
        host.ReleaseFirstRead();
        await failed.ConfigureAwait(true);
        await next.ConfigureAwait(true);

        relay.Page.Devices.Rows.Select(row => row.Name).ShouldBe(["Office Mac"]);
    }

    /// <summary>A host whose first device read waits until the test releases it, then fails with the given exception when one is set; later reads answer at once with a different device.</summary>
    private sealed class SlowFirstReadHost(Exception? firstReadFailure = null) : IDispatchHost
    {
        private readonly TaskCompletionSource _firstRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _reads;

        public void ReleaseFirstRead() => _firstRead.SetResult();

        public async Task<IReadOnlyList<DispatchDevice>> ListDevicesAsync(CancellationToken cancellationToken)
        {
            _reads++;
            if (_reads == 1)
            {
                await _firstRead.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
                if (firstReadFailure is not null)
                {
                    throw firstReadFailure;
                }

                return [new DispatchDevice("device-old", "Old laptop", Online: true, PresenceKnown: true)];
            }

            return [new DispatchDevice("device-new", "Office Mac", Online: true, PresenceKnown: true)];
        }

        public Task<IReadOnlyList<DispatchCommand>> ListCommandsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DispatchCommand>>([]);

        public Task RenameDeviceAsync(string deviceId, string name, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RevokeDeviceAsync(string deviceId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RevokeAllDevicesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<string> SendAsync(string deviceId, string operation, string text, CancellationToken cancellationToken) =>
            Task.FromException<string>(new NotSupportedException());

        public Task CancelAsync(string commandId, CancellationToken cancellationToken) => Task.CompletedTask;

        public async IAsyncEnumerable<DispatchFeedChange> WatchFeedAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(true);
            yield break;
        }
    }
}
