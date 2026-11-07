using System.Text.Json;
using Autonogrammer.Contracts.Cowork.Browser;
using PuppeteerSharp;

namespace Autonogrammer.Harness.BrowserUse.Viewer;

/// <summary>
/// Pulls <c>Page.screencastFrame</c> for one desktop viewer. A frame is acknowledged only after the viewer consumes it.
/// A burst is dropped, never queued. With no viewer, the pump sends <c>Page.stopScreencast</c>.
/// </summary>
public sealed class ScreencastPump
{
    /// <summary>Chrome holds at most this many frames until <c>screencastFrameAck</c>.</summary>
    public const int MaxFramesInFlight = 1;

    private readonly Lock _gate = new();
    private ICDPSession? _session;
    private EventHandler<MessageEventArgs>? _handler;
    private Func<BrowserFrame, Task>? _consume;
    private int _viewers;
    private int _inFlight;

    /// <summary>Gets how many viewers are attached.</summary>
    public int ViewerCount
    {
        get
        {
            lock (_gate)
            {
                return _viewers;
            }
        }
    }

    /// <summary>
    /// Attaches a viewer and starts the screencast on the first one. <paramref name="sendLastFrame"/> is sent so a late viewer receives the last frame.
    /// </summary>
    /// <param name="session">The page session.</param>
    /// <param name="consume">Called when the viewer may take the frame. The ack waits for this task.</param>
    /// <param name="quality">The JPEG quality.</param>
    /// <param name="longEdge">The maximum long edge, in pixels.</param>
    /// <param name="sendLastFrame">True to ask Chrome for the last frame.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The task.</returns>
    public async Task AddViewerAsync(
        ICDPSession session,
        Func<BrowserFrame, Task> consume,
        int quality,
        int longEdge,
        bool sendLastFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(consume);
        cancellationToken.ThrowIfCancellationRequested();
        var start = false;
        lock (_gate)
        {
            _session = session;
            _consume = consume;
            _viewers++;
            start = _viewers == 1;
            if (start)
            {
                _handler = (_, args) => OnMessage(args);
                session.MessageReceived += _handler;
            }
        }

        if (!start)
        {
            return;
        }

        await session.SendAsync(
            "Page.startScreencast",
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["format"] = "jpeg",
                ["quality"] = quality,
                ["maxWidth"] = longEdge,
                ["maxHeight"] = longEdge,
                ["maxFramesInFlight"] = MaxFramesInFlight,
                ["sendLastFrame"] = sendLastFrame,
            },
            waitForCallback: true,
            options: null).ConfigureAwait(false);
    }

    /// <summary>Detaches one viewer. The last viewer stops the screencast.</summary>
    /// <param name="cancellationToken">Cancels the stop.</param>
    /// <returns>The task.</returns>
    public async Task RemoveViewerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ICDPSession? session;
        EventHandler<MessageEventArgs>? handler;
        var stop = false;
        lock (_gate)
        {
            if (_viewers == 0)
            {
                return;
            }

            _viewers--;
            stop = _viewers == 0;
            session = _session;
            handler = _handler;
            if (stop)
            {
                _consume = null;
                _handler = null;
            }
        }

        if (!stop || session is null)
        {
            return;
        }

        if (handler is not null)
        {
            session.MessageReceived -= handler;
        }

        await session.SendAsync("Page.stopScreencast", new Dictionary<string, object>(StringComparer.Ordinal), waitForCallback: true, options: null).ConfigureAwait(false);
    }

    private void OnMessage(MessageEventArgs args)
    {
        if (!string.Equals(args.MessageID, "Page.screencastFrame", StringComparison.Ordinal))
        {
            return;
        }

        if (!TryRead(args.MessageData, out var frame, out var sessionId))
        {
            return;
        }

        var drop = false;
        Func<BrowserFrame, Task>? consume;
        ICDPSession? session;
        lock (_gate)
        {
            session = _session;
            consume = _consume;
            if (session is null || consume is null || _viewers == 0)
            {
                drop = true;
            }
            else if (_inFlight >= MaxFramesInFlight)
            {
                drop = true;
            }
            else
            {
                _inFlight++;
            }
        }

        if (session is null)
        {
            return;
        }

        if (drop || consume is null)
        {
            _ = AckAsync(session, sessionId);
            return;
        }

        _ = DeliverAsync(session, consume, frame, sessionId);
    }

    private async Task DeliverAsync(ICDPSession session, Func<BrowserFrame, Task> consume, BrowserFrame frame, int sessionId)
    {
        try
        {
            await consume(frame).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                _inFlight = Math.Max(0, _inFlight - 1);
            }

            await AckAsync(session, sessionId).ConfigureAwait(false);
        }
    }

    private static async Task AckAsync(ICDPSession session, int sessionId)
    {
        await session.SendAsync(
            "Page.screencastFrameAck",
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["sessionId"] = sessionId,
            },
            waitForCallback: true,
            options: null).ConfigureAwait(false);
    }

    private static bool TryRead(JsonElement data, out BrowserFrame frame, out int sessionId)
    {
        frame = null!;
        sessionId = 0;
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("sessionId", out var id) || !data.TryGetProperty("data", out var bytes))
        {
            return false;
        }

        sessionId = id.ValueKind == JsonValueKind.Number ? id.GetInt32() : int.Parse(id.GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
        var jpeg = Convert.FromBase64String(bytes.GetString() ?? string.Empty);
        var metadata = data.TryGetProperty("metadata", out var meta) ? meta : default;
        frame = new BrowserFrame(jpeg, ReadMetadata(metadata), sessionId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return true;
    }

    private static BrowserFrameMetadata ReadMetadata(JsonElement metadata)
    {
        return new BrowserFrameMetadata(
            Number(metadata, "offsetTop"),
            Number(metadata, "pageScaleFactor"),
            Number(metadata, "deviceWidth"),
            Number(metadata, "deviceHeight"),
            Number(metadata, "scrollOffsetX"),
            Number(metadata, "scrollOffsetY"),
            Number(metadata, "timestamp"));
    }

    private static double Number(JsonElement metadata, string name)
    {
        return metadata.ValueKind == JsonValueKind.Object && metadata.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : 0;
    }
}
