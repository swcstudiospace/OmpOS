using System.Globalization;
using System.Security.Cryptography;
using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Frames;

/// <summary>
/// Capture, mask, downscale, PNG, byte cap and the session image budget.
/// Masking is defence in depth. One display is captured at a time. Layout changes become <see cref="DenialCode.StaleFrame"/>.
/// </summary>
public sealed class VisionPipeline
{
    private readonly ComputerUseOptions _options;
    private readonly IComputerUseBackend _backend;
    private readonly TimeProvider _timeProvider;
    private readonly string? _modelId;
    private readonly Dictionary<string, SessionFrames> _sessions = new(StringComparer.Ordinal);

    /// <summary>Creates the pipeline.</summary>
    /// <param name="options">Validated options. Tests may set a zero settle.</param>
    /// <param name="backend">Capture and pointer reads. This type does not inject.</param>
    /// <param name="time">Clock for capture time and zoom retention.</param>
    /// <param name="modelId">Model id for <see cref="ComputerUseOptions.ModelOverrides"/>, or null.</param>
    public VisionPipeline(ComputerUseOptions options, IComputerUseBackend backend, TimeProvider time, string? modelId)
    {
        _options = options;
        _backend = backend;
        _timeProvider = time;
        _modelId = modelId;
    }

    /// <summary>Tokens for a scaled frame. <c>ceil(w/28)*ceil(h/28)</c>.</summary>
    /// <param name="width">Width.</param>
    /// <param name="height">Height.</param>
    /// <returns>The token count.</returns>
    public static int Tokens(int width, int height) =>
        (int)Math.Ceiling(width / 28d) * (int)Math.Ceiling(height / 28d);

    /// <summary>Long edge for the model, or the default.</summary>
    public int LongEdge
    {
        get
        {
            if (_modelId is not null && _options.ModelOverrides.TryGetValue(_modelId, out int edge))
            {
                return edge;
            }

            return _options.LongEdgePx;
        }
    }

    /// <summary>Captures one display. The 81st image of a default session is <see cref="DenialCode.ImageBudget"/>.</summary>
    /// <param name="sessionId">Session id.</param>
    /// <param name="display">Display to capture. One display at a time.</param>
    /// <param name="windows">Z-order used for masking.</param>
    /// <param name="grantedApps">Granted application identities.</param>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns>The capture.</returns>
    public async ValueTask<VisionCapture> CaptureAsync(
        string sessionId,
        DisplayInfo display,
        IReadOnlyList<WindowInfo> windows,
        IReadOnlySet<string> grantedApps,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(display);
        SessionFrames session = Session(sessionId);
        if (session.Count >= _options.MaxImagesPerSession)
        {
            return new VisionCapture(null, DenialCode.ImageBudget);
        }

        string layout = LayoutHash(windows);
        if (session.Layout is not null && !string.Equals(session.Layout, layout, StringComparison.Ordinal))
        {
            session.Layout = layout;
            return new VisionCapture(null, DenialCode.StaleFrame);
        }

        if (_options.SettleMs > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(_options.SettleMs), _timeProvider, cancellationToken).ConfigureAwait(false);
        }

        RawFrame raw = await _backend.CaptureAsync(
            new CaptureRegion { DisplayId = display.Id, Bounds = display.Bounds },
            cancellationToken).ConfigureAwait(false);
        BgraImage native = Unpack(raw);
        BgraImage masked = VisibleRegionMasker.Mask(native, display.Bounds, windows, grantedApps);
        BgraImage scaled = FrameScaler.Downscale(masked, LongEdge);
        PngEncodeResult encoded = PngEncoder.Encode(scaled, _options.MaxImageBytes);
        if (encoded.Denial is DenialCode denial)
        {
            return new VisionCapture(null, denial);
        }

        string hash = Convert.ToHexString(SHA256.HashData(encoded.Bytes.Span));
        session.Hashes.Add(hash);
        string? hint = Identical(session.Hashes) ? "three-identical" : null;
        PointerState pointer = await _backend.GetPointerAsync(cancellationToken).ConfigureAwait(false);
        ModelPoint model = CoordinateMap.ScaleToModel(display, new NativePoint { X = pointer.X, Y = pointer.Y });
        session.Count++;
        session.Layout = layout;
        FrameRecord frame = new()
        {
            Id = sessionId + ":" + session.Count.ToString(CultureInfo.InvariantCulture),
            DisplayId = display.Id,
            NativeWidth = native.Width,
            NativeHeight = native.Height,
            ScaledWidth = scaled.Width,
            ScaledHeight = scaled.Height,
            ScaleFactor = display.ScaleFactor,
            Cursor = model.X.ToString(CultureInfo.InvariantCulture) + "," + model.Y.ToString(CultureInfo.InvariantCulture),
            Sha256 = hash,
            Png = encoded.Bytes!,
            CapturedAt = _timeProvider.GetUtcNow(),
            Number = session.Count,
            IdenticalHashHint = hint,
        };
        session.Latest = frame;
        session.Touched = _timeProvider.GetUtcNow();
        return new VisionCapture(frame, null);
    }

    /// <summary>Latest frame kept for zoom, or null after 60 seconds idle.</summary>
    /// <param name="sessionId">Session id.</param>
    /// <returns>The frame, or null.</returns>
    public FrameRecord? LatestForZoom(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out SessionFrames? session) || session.Latest is null)
        {
            return null;
        }

        if (_timeProvider.GetUtcNow() - session.Touched >= TimeSpan.FromSeconds(60))
        {
            session.Latest = null;
            return null;
        }

        return session.Latest;
    }

    /// <summary>Drops retained frames for the session.</summary>
    /// <param name="sessionId">Session id.</param>
    public void Release(string sessionId) => _sessions.Remove(sessionId);

    private SessionFrames Session(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out SessionFrames? session))
        {
            session = new SessionFrames();
            _sessions[sessionId] = session;
        }

        return session;
    }

    private static BgraImage Unpack(RawFrame raw)
    {
        byte[] pixels = new byte[raw.Width * raw.Height * 4];
        ReadOnlySpan<byte> source = raw.Pixels.Span;
        for (int y = 0; y < raw.Height; y++)
        {
            source.Slice(y * raw.Stride, raw.Width * 4).CopyTo(pixels.AsSpan(y * raw.Width * 4));
        }

        return new BgraImage(raw.Width, raw.Height, pixels);
    }

    private static string LayoutHash(IReadOnlyList<WindowInfo> windows)
    {
        string text = string.Join('|', windows.Select(static window => window.Id + ":" + window.Bounds.X + "," + window.Bounds.Y));
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
    }

    private static bool Identical(List<string> hashes)
    {
        int n = hashes.Count;
        return n >= 3
            && string.Equals(hashes[n - 1], hashes[n - 2], StringComparison.Ordinal)
            && string.Equals(hashes[n - 1], hashes[n - 3], StringComparison.Ordinal);
    }

    private sealed class SessionFrames
    {
        public int Count { get; set; }

        public string? Layout { get; set; }

        public List<string> Hashes { get; } = [];

        public FrameRecord? Latest { get; set; }

        public DateTimeOffset Touched { get; set; }
    }
}
