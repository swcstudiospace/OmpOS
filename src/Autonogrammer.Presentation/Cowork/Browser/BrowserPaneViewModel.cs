#pragma warning disable MA0048 // The plan owns this file for the pane model and its command records.
using Autonogrammer.Contracts.Cowork.Browser;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Cowork.Browser;

/// <summary>
/// A model the existing catalogue already marked with <c>OmpModelInfo.AcceptsImages</c>.
/// <c>CoworkSessionContext.ModelReadsImages</c> is that same fact for the current session.
/// This record adds no image flag to a catalogue type.
/// </summary>
/// <param name="Selector">The model selector.</param>
/// <param name="AcceptsImages">The catalogue value, copied from <c>OmpModelInfo.AcceptsImages</c>.</param>
public sealed record BrowserListedModel(string Selector, bool AcceptsImages);

/// <summary>A keyboard-operable pane command. Release is what the pane view calls on F6.</summary>
/// <param name="Name">The command name.</param>
/// <param name="AccessKey">The access key. Release uses F6.</param>
public sealed record BrowserPaneCommand(string Name, string AccessKey);

/// <summary>
/// The framework-free browser pane. It binds only to the feed, the control and tool-result bytes.
/// </summary>
public sealed class BrowserPaneViewModel : ObservableModel, IDockPanel, IDisposable
{
    private readonly IBrowserPaneFeed _feed;
    private readonly IBrowserPaneControl _control;
    private readonly TimeProvider _time;
    private DateTimeOffset? _lastAnnouncement;
    private IReadOnlyList<BrowserListedModel> _listed = [];

    /// <summary>Initializes the pane over a feed and a control.</summary>
    /// <param name="feed">The state and frames.</param>
    /// <param name="control">The desktop commands. Remote heads pass a control that refuses input.</param>
    /// <param name="time">The clock that coalesces announcements. Null uses <see cref="TimeProvider.System"/>.</param>
    public BrowserPaneViewModel(IBrowserPaneFeed feed, IBrowserPaneControl control, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(control);
        _feed = feed;
        _control = control;
        _time = time ?? TimeProvider.System;
        _feed.Changed += OnFeedChanged;
    }

    /// <summary>The commands a keyboard can invoke. Release is F6.</summary>
    public static IReadOnlyList<BrowserPaneCommand> Commands { get; } =
    [
        new("Take over", "T"),
        new("Release", "F6"),
        new("Stop", "S"),
        new("Close", "C"),
    ];

    /// <inheritdoc />
    public string Id => "browser";

    /// <inheritdoc />
    public string Title => "Browser";

    /// <inheritdoc />
    public object Content => this;

    /// <summary>Gets the pane state.</summary>
    public BrowserPaneState State => _feed.State;

    /// <summary>Gets the newest frame.</summary>
    public BrowserFrame? LatestFrame => _feed.LatestFrame;

    /// <summary>Gets the one sentence for <see cref="State"/>.</summary>
    public string Sentence => BrowserPaneCopy.Sentence(State);

    /// <summary>Gets the one fix action for <see cref="State"/>.</summary>
    public string FixAction => BrowserPaneCopy.FixAction(State);

    /// <summary>Gets the last announcement. Further state changes inside two seconds do not replace it.</summary>
    public string? Announcement { get; private set; }

    /// <summary>Gets how many announcements were published.</summary>
    public int Announcements { get; private set; }

    /// <summary>Gets the chrome sentence while the agent can actuate. Empty otherwise. It is not drawn inside the page frame.</summary>
    public string HeaderSentence => _control.Control == BrowserControlState.Agent ? BrowserChromeCopy.AgentControlling : string.Empty;

    /// <summary>Gets whether the header offers Stop.</summary>
    public bool HeaderOffersStop => _control.Control != BrowserControlState.Revoked;

    /// <summary>Gets whether the header offers Close browser.</summary>
    public bool HeaderOffersClose => State != BrowserPaneState.Stopped;

    /// <summary>Gets the byte count of the last tool image.</summary>
    public int LastImageByteCount { get; private set; }

    /// <summary>Gets the sniffed format of the last tool image.</summary>
    public string? LastImageFormat { get; private set; }

    /// <summary>Gets the width read from the last tool image bytes, or zero when the bytes have no size.</summary>
    public int LastImageWidth { get; private set; }

    /// <summary>Gets the height read from the last tool image bytes, or zero when the bytes have no size.</summary>
    public int LastImageHeight { get; private set; }

    /// <summary>Gets when the last tool image bytes were noted.</summary>
    public DateTimeOffset? LastImageTime { get; private set; }

    /// <summary>
    /// Gets selectors whose catalogue row already has <c>OmpModelInfo.AcceptsImages</c>.
    /// The head fills <see cref="SetListedModels"/> from that catalogue. <c>CoworkSessionContext.ModelReadsImages</c> is the session's copy of the same fact.
    /// </summary>
    public IReadOnlyList<string> VisionSelectors => _listed.Where(model => model.AcceptsImages).Select(model => model.Selector).ToArray();

    /// <summary>Records catalogue rows. Only rows that already accept images are offered for <see cref="BrowserPaneState.ModelCannotSeeImages"/>.</summary>
    /// <param name="models">The rows. <see cref="BrowserListedModel.AcceptsImages"/> is copied from <c>OmpModelInfo.AcceptsImages</c>.</param>
    public void SetListedModels(IReadOnlyList<BrowserListedModel> models)
    {
        ArgumentNullException.ThrowIfNull(models);
        _listed = models;
        NotifyPropertyChanged(nameof(VisionSelectors));
    }

    /// <summary>Notes image bytes the tool returned. Size, format, byte count and time come from those bytes and the clock.</summary>
    /// <param name="bytes">The returned image.</param>
    public void ShowReturnedImage(ReadOnlyMemory<byte> bytes)
    {
        LastImageByteCount = bytes.Length;
        LastImageFormat = bytes.Length >= 2 && bytes.Span[0] == 0xFF && bytes.Span[1] == 0xD8 ? "jpeg" : "unknown";
        (LastImageWidth, LastImageHeight) = ReadJpegSize(bytes.Span);
        LastImageTime = _time.GetUtcNow();
        NotifyPropertyChanged(nameof(LastImageByteCount));
        NotifyPropertyChanged(nameof(LastImageFormat));
        NotifyPropertyChanged(nameof(LastImageWidth));
        NotifyPropertyChanged(nameof(LastImageHeight));
        NotifyPropertyChanged(nameof(LastImageTime));
    }

    /// <summary>Invokes a <see cref="Commands"/> entry by name.</summary>
    /// <param name="name">The command name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The task.</returns>
    public Task InvokeCommandAsync(string name, CancellationToken cancellationToken = default)
    {
        return name switch
        {
            "Take over" => TakeOverAsync(cancellationToken),
            "Release" => ReleaseControlAsync(cancellationToken),
            "Stop" => StopAsync(cancellationToken),
            "Close" => CloseBrowserAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown pane command."),
        };
    }

    /// <summary>Takes over through the control.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The task.</returns>
    public Task TakeOverAsync(CancellationToken cancellationToken = default) => _control.TakeOverAsync(cancellationToken);

    /// <summary>Releases keyboard capture through the control. The pane view calls this on F6.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The task.</returns>
    public Task ReleaseControlAsync(CancellationToken cancellationToken = default) => _control.ReleaseControlAsync(cancellationToken);

    /// <summary>Forwards a pointer event to the control.</summary>
    /// <param name="input">The pointer event.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The task.</returns>
    public Task SendPointerAsync(BrowserPointerInput input, CancellationToken cancellationToken = default) =>
        _control.SendPointerAsync(input, cancellationToken);

    /// <summary>Forwards a key event to the control.</summary>
    /// <param name="input">The key event.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The task.</returns>
    public Task SendKeyAsync(BrowserKeyInput input, CancellationToken cancellationToken = default) =>
        _control.SendKeyAsync(input, cancellationToken);

    /// <summary>Stops through the control, which cancels in-flight calls and revokes control.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The task.</returns>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _control.StopAsync(cancellationToken).ConfigureAwait(false);
        NotifyChrome();
    }

    /// <summary>Closes the browser through the control.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The task.</returns>
    public Task CloseBrowserAsync(CancellationToken cancellationToken = default) => _control.CloseBrowserAsync(cancellationToken);

    /// <inheritdoc />
    public void Dispose() => _feed.Changed -= OnFeedChanged;

    private void OnFeedChanged(object? sender, EventArgs e)
    {
        Announce(Sentence);
        NotifyPropertyChanged(nameof(State));
        NotifyPropertyChanged(nameof(Sentence));
        NotifyPropertyChanged(nameof(FixAction));
        NotifyPropertyChanged(nameof(LatestFrame));
        NotifyPropertyChanged(nameof(VisionSelectors));
    }

    private void Announce(string sentence)
    {
        var now = _time.GetUtcNow();
        if (_lastAnnouncement is { } last && now - last < TimeSpan.FromSeconds(2))
        {
            return;
        }

        _lastAnnouncement = now;
        Announcement = sentence;
        Announcements++;
        NotifyPropertyChanged(nameof(Announcement));
        NotifyPropertyChanged(nameof(Announcements));
    }

    private void NotifyChrome()
    {
        NotifyPropertyChanged(nameof(HeaderSentence));
        NotifyPropertyChanged(nameof(HeaderOffersStop));
        NotifyPropertyChanged(nameof(HeaderOffersClose));
    }

    private static (int Width, int Height) ReadJpegSize(ReadOnlySpan<byte> bytes)
    {
        for (var i = 0; i < bytes.Length - 8; i++)
        {
            if (bytes[i] != 0xFF || (bytes[i + 1] != 0xC0 && bytes[i + 1] != 0xC2))
            {
                continue;
            }

            var height = (bytes[i + 5] << 8) | bytes[i + 6];
            var width = (bytes[i + 7] << 8) | bytes[i + 8];
            return (width, height);
        }

        return (0, 0);
    }
}
