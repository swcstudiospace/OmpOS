using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Api.Dispatch;
using Autonogrammer.Api.Dispatch.Devices;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Sessions.Postgres;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Security;

/// <summary>
/// The send order of <see cref="DispatchDeliveryLoop"/>. A queued command moves to Delivered before its signed frame is written, so a frame is never
/// on the wire for a command the store did not record as delivered. An erasure refusal from that transition stops the send. The loop runs against a
/// store that records each transition and a socket over a recording stream, so the recorded order is the order the loop used.
/// </summary>
public sealed class DispatchSendOrderTests
{
    private const string AccountId = "acct";
    private const string DeviceId = "dev-1";
    private const string CommandId = "cmd-1";
    private const string Transition = "transition";
    private const string Send = "send";
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    /// <summary>When the transition is applied, it is recorded before the signed frame is written, and the frame carries the command.</summary>
    [Fact]
    public async Task AppliedTransition_IsRecordedBeforeTheSignedFrameIsSent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var rig = new Rig(refusal: null);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var running = rig.Loop.RunAsync(rig.Session, 0, stop.Token);

        await rig.Stream.WaitForFirstWriteAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(true);
        await stop.CancelAsync().ConfigureAwait(true);
        await running.ConfigureAwait(true);

        var order = rig.Events.ToArray();
        order.ShouldContain(Send, StringComparer.Ordinal, "the signed frame is written");
        order[0].ShouldBe(Transition, "the transition is recorded before any frame byte is written");
        rig.Store.StateOf(CommandId).ShouldBe(DispatchCommandState.Delivered);
        Encoding.UTF8.GetString(rig.Stream.Sent()).ShouldContain(CommandId, Case.Sensitive, "the written frame is the queued command");
    }

    /// <summary>When the transition is refused by erasure, the loop stops and writes no frame.</summary>
    [Fact]
    public async Task RefusedTransition_WritesNoFrame()
    {
        var ct = TestContext.Current.CancellationToken;
        using var rig = new Rig(new AccountErasingException("The account is being erased."));
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(TimeSpan.FromSeconds(10));

        await rig.Loop.RunAsync(rig.Session, 0, bound.Token).ConfigureAwait(true);

        var order = rig.Events.ToArray();
        order.ShouldContain(Transition, StringComparer.Ordinal, "the refused transition was attempted");
        order.ShouldNotContain(Send, StringComparer.Ordinal, "no frame is written after the refusal");
        rig.Stream.Sent().ShouldBeEmpty("the refused command is not on the wire");
        rig.Store.StateOf(CommandId).ShouldBe(DispatchCommandState.Queued, "the refused command stays queued");
    }

    /// <summary>
    /// One device session, its signer and its loop, over a store holding one queued command. The store and the socket append to one ordered log.
    /// </summary>
    private sealed class Rig : IDisposable
    {
        private readonly ECDsa _credentialKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly ECDsa _commandKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly DeviceCredentialIssuer _issuer;
        private readonly WebSocket _socket;

        /// <summary>Builds the rig. A non-null refusal is thrown by the store's transition.</summary>
        /// <param name="refusal">The exception the store throws on transition, or null for an applied transition.</param>
        public Rig(Exception? refusal)
        {
            var time = new FixedTimeProvider(Now);
            var options = new DispatchOptions
            {
                Credentials = { SigningKeyPem = _credentialKey.ExportPkcs8PrivateKeyPem() },
                CommandSigning = { KeyPem = _commandKey.ExportPkcs8PrivateKeyPem() },
            };
            _issuer = new DeviceCredentialIssuer(Microsoft.Extensions.Options.Options.Create(options), time);
            var command = new DispatchCommandRecord(
                AccountId,
                DeviceId,
                CommandId,
                1,
                DispatchCommandState.Queued,
                DispatchCapability.Sessions,
                DispatchOp.SessionList,
                "fingerprint",
                null,
                "{\"prompt\":\"hello\"}",
                Now,
                Now.AddMinutes(5),
                null);
            Store = new RecordingDispatchStore(Events, refusal, command);
            Stream = new RecordingStream(Events);
            _socket = WebSocket.CreateFromStream(Stream, new WebSocketCreationOptions { IsServer = true, KeepAliveInterval = Timeout.InfiniteTimeSpan });
            Session = new DispatchHubSession(AccountId, DeviceId, "holder", _socket);
            Loop = new DispatchDeliveryLoop(Store, new DispatchSigner(_issuer), time);
        }

        /// <summary>Gets the ordered log shared by the store and the socket.</summary>
        public ConcurrentQueue<string> Events { get; } = new();

        /// <summary>Gets the fake store.</summary>
        public RecordingDispatchStore Store { get; }

        /// <summary>Gets the recording stream under the socket.</summary>
        public RecordingStream Stream { get; }

        /// <summary>Gets the device session the loop sends on.</summary>
        public DispatchHubSession Session { get; }

        /// <summary>Gets the delivery loop under test.</summary>
        public DispatchDeliveryLoop Loop { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            Session.Dispose();
            _socket.Dispose();
            _issuer.Dispose();
            _commandKey.Dispose();
            _credentialKey.Dispose();
        }
    }

    /// <summary>
    /// A store with one queued command. It records each transition into the shared log, and throws the refusal instead of applying it when one is set.
    /// Only the queue listing and the transition are used by the delivery loop; the other members are not reached from this suite.
    /// </summary>
    private sealed class RecordingDispatchStore : IDispatchStore
    {
        private readonly ConcurrentQueue<string> _events;
        private readonly Exception? _refusal;
        private readonly List<DispatchCommandRecord> _commands;

        /// <summary>Initializes a new instance of the <see cref="RecordingDispatchStore"/> class.</summary>
        /// <param name="events">The shared ordered log.</param>
        /// <param name="refusal">The exception a transition throws, or null to apply it.</param>
        /// <param name="command">The queued command.</param>
        public RecordingDispatchStore(ConcurrentQueue<string> events, Exception? refusal, DispatchCommandRecord command)
        {
            _events = events;
            _refusal = refusal;
            _commands = [command];
        }

        /// <summary>Returns the state the store holds for one command.</summary>
        /// <param name="commandId">The command.</param>
        /// <returns>The stored state.</returns>
        public DispatchCommandState StateOf(string commandId) => _commands.Single(c => string.Equals(c.CommandId, commandId, StringComparison.Ordinal)).State;

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<DispatchCommandRecord>> ListQueuedAfterAsync(string accountId, string deviceId, long afterSeq, CancellationToken cancellationToken)
        {
            IReadOnlyList<DispatchCommandRecord> queued = _commands
                .Where(c => string.Equals(c.AccountId, accountId, StringComparison.Ordinal) && string.Equals(c.DeviceId, deviceId, StringComparison.Ordinal) && c.State == DispatchCommandState.Queued && c.Seq > afterSeq)
                .ToArray();
            return ValueTask.FromResult(queued);
        }

        /// <inheritdoc />
        public ValueTask<DispatchTransitionResult> TryTransitionAsync(string accountId, string commandId, DispatchCommandState expected, DispatchCommandState desired, DispatchFailureReason? reason, CancellationToken cancellationToken)
        {
            _events.Enqueue(Transition);
            if (_refusal is not null)
            {
                return ValueTask.FromException<DispatchTransitionResult>(_refusal);
            }

            var index = _commands.FindIndex(c => string.Equals(c.AccountId, accountId, StringComparison.Ordinal) && string.Equals(c.CommandId, commandId, StringComparison.Ordinal) && c.State == expected);
            if (index < 0)
            {
                return ValueTask.FromResult(new DispatchTransitionResult(DispatchTransitionStatus.NotFound, null));
            }

            _commands[index] = _commands[index] with { State = desired };
            return ValueTask.FromResult(new DispatchTransitionResult(DispatchTransitionStatus.Applied, desired));
        }

        /// <inheritdoc />
        public ValueTask<DispatchDeviceCreateResult> CreateDeviceAsync(CreateDispatchDevice request, CancellationToken cancellationToken) => throw Unused(nameof(CreateDeviceAsync));

        /// <inheritdoc />
        public ValueTask<DispatchDeviceRecord?> GetDeviceAsync(string accountId, string deviceId, CancellationToken cancellationToken) => throw Unused(nameof(GetDeviceAsync));

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<DispatchDeviceRecord>> ListDevicesAsync(string accountId, CancellationToken cancellationToken) => throw Unused(nameof(ListDevicesAsync));

        /// <inheritdoc />
        public ValueTask<bool> RenameDeviceAsync(string accountId, string deviceId, string name, CancellationToken cancellationToken) => throw Unused(nameof(RenameDeviceAsync));

        /// <inheritdoc />
        public ValueTask<bool> RevokeDeviceAsync(string accountId, string deviceId, string? reason, DateTimeOffset revokedAt, CancellationToken cancellationToken) => throw Unused(nameof(RevokeDeviceAsync));

        /// <inheritdoc />
        public ValueTask<bool> UpdateCredentialJtiAsync(string accountId, string deviceId, string credentialJti, DateTimeOffset credentialExpiresAt, CancellationToken cancellationToken) => throw Unused(nameof(UpdateCredentialJtiAsync));

        /// <inheritdoc />
        public ValueTask<bool> UpdateCapabilitySnapshotAsync(string accountId, string deviceId, IReadOnlyList<DispatchCapabilitySnapshot> capabilities, CancellationToken cancellationToken) => throw Unused(nameof(UpdateCapabilitySnapshotAsync));

        /// <inheritdoc />
        public ValueTask<bool> TouchLastSeenAsync(string accountId, string deviceId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken) => throw Unused(nameof(TouchLastSeenAsync));

        /// <inheritdoc />
        public ValueTask<bool> UpdatePairedSessionAsync(string accountId, string deviceId, string pairedSessionId, CancellationToken cancellationToken) => throw Unused(nameof(UpdatePairedSessionAsync));

        /// <inheritdoc />
        public ValueTask<DispatchEnqueueResult> EnqueueAsync(EnqueueDispatchCommand request, CancellationToken cancellationToken) => throw Unused(nameof(EnqueueAsync));

        /// <inheritdoc />
        public ValueTask<DispatchEnqueueResult?> ReplayAsync(EnqueueDispatchCommand request, CancellationToken cancellationToken) => throw Unused(nameof(ReplayAsync));

        /// <inheritdoc />
        public ValueTask<DispatchCommandRecord?> GetCommandAsync(string accountId, string commandId, CancellationToken cancellationToken) => throw Unused(nameof(GetCommandAsync));

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<DispatchCommandRecord>> ListCommandsAsync(string accountId, string? deviceId, int limit, CancellationToken cancellationToken) => throw Unused(nameof(ListCommandsAsync));

        /// <inheritdoc />
        public ValueTask<DispatchFeedEventRecord> AppendFeedEventAsync(string accountId, string kind, string? payload, CancellationToken cancellationToken) => throw Unused(nameof(AppendFeedEventAsync));

        /// <inheritdoc />
        public ValueTask<DispatchFeedPage> ReadFeedAsync(string accountId, long afterId, CancellationToken cancellationToken) => throw Unused(nameof(ReadFeedAsync));

        private static NotSupportedException Unused(string member) => new($"{member} is not reached by the delivery loop in this suite.");
    }

    /// <summary>
    /// The stream under a server-side socket. Every write is recorded into the shared log and into the bytes the socket sent. The first write completes
    /// the wait in <see cref="WaitForFirstWriteAsync"/>. A read reports end of stream, because the loop never receives.
    /// </summary>
    private sealed class RecordingStream : Stream
    {
        private readonly ConcurrentQueue<string> _events;
        private readonly MemoryStream _sent = new();
        private readonly TaskCompletionSource<bool> _firstWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Lock _gate = new();

        /// <summary>Initializes a new instance of the <see cref="RecordingStream"/> class.</summary>
        /// <param name="events">The shared ordered log.</param>
        public RecordingStream(ConcurrentQueue<string> events)
        {
            _events = events;
        }

        /// <summary>Waits for the first byte written to the socket.</summary>
        /// <param name="timeout">The longest wait for the first write.</param>
        /// <param name="cancellationToken">Cancels the wait.</param>
        /// <returns>A task that completes at the first write, or fails when the wait ends first.</returns>
        public Task<bool> WaitForFirstWriteAsync(TimeSpan timeout, CancellationToken cancellationToken) => _firstWrite.Task.WaitAsync(timeout, cancellationToken);

        /// <summary>Returns a copy of every byte the socket has written.</summary>
        /// <returns>The written bytes.</returns>
        public byte[] Sent()
        {
            lock (_gate)
            {
                return _sent.ToArray();
            }
        }

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => true;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => 0;

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => Record(buffer.AsSpan(offset, count));

        /// <inheritdoc />
        public override void Write(ReadOnlySpan<byte> buffer) => Record(buffer);

        /// <inheritdoc />
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Record(buffer.Span);
            return ValueTask.CompletedTask;
        }

        private void Record(ReadOnlySpan<byte> bytes)
        {
            _events.Enqueue(Send);
            lock (_gate)
            {
                _sent.Write(bytes);
            }

            _ = _firstWrite.TrySetResult(true);
        }
    }
}
