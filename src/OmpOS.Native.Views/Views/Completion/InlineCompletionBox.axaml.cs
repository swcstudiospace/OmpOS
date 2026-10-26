using System.Runtime.ExceptionServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using OmpOS.Harness.Completion;
using OmpOS.Native.Views.Blocks;

namespace OmpOS.Native.Views.Completion;

/// <summary>
/// A text box that shows a model suggestion after its caret as muted ghost text. Tab accepts the suggestion and inserts it
/// at the caret; Escape dismisses it and inserts nothing.
/// </summary>
/// <remarks>
/// <para>
/// A request is sent only when <see cref="Client"/> is set and its settings configure a provider. A request is made for a
/// text and caret position not asked about before, so a burst of typing sends one request per settled position. Every edit
/// or caret move cancels the pending request and starts a new one after <see cref="DebounceInterval"/>. Accepting a
/// suggestion and dismissing it send no request; a dismissed position is not asked about again until the text or caret moves.
/// </para>
/// <para>
/// While a suggestion shows, the text box's own text is drawn transparent and a mirror of the whole text is drawn over it,
/// with the suggestion in the muted colour. The caret stays in the text box, so the suggestion sits at the caret even when
/// text follows it. With no suggestion shown, Tab and Escape keep their usual meaning, so focus still moves with Tab.
/// </para>
/// </remarks>
internal partial class InlineCompletionBox : UserControl
{
    private const string ShownHelp = "Press Tab to accept the suggested completion, or Escape to dismiss it.";

    private CompletionClient? _client;
    private TimeSpan _debounce = TimeSpan.FromMilliseconds(250);
    private CancellationTokenSource? _pending;
    private CaretBuffer? _lastAsked;
    private ShownSuggestion? _shown;
    private IBrush? _inputBrush;
    private bool _applying;

    /// <summary>Creates the control with no provider, so it never sends a request until <see cref="Client"/> is set.</summary>
    public InlineCompletionBox()
    {
        InitializeComponent();
        InputBox.PropertyChanged += OnInputPropertyChanged;
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>Gets or sets the completion client. Null, or a client whose settings configure no provider, shows no suggestions.</summary>
    /// <remarks>Setting the client discards any shown suggestion and any pending request. No request is sent until the next edit.</remarks>
    public CompletionClient? Client
    {
        get => _client;
        set
        {
            _client = value;
            _lastAsked = null;
            CancelPending();
            ClearSuggestion();
            UnavailableText.IsVisible = false;
        }
    }

    /// <summary>Gets or sets how long the text must stay unchanged before a request is sent. Must not be negative.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The interval is negative.</exception>
    public TimeSpan DebounceInterval
    {
        get => _debounce;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
            _debounce = value;
        }
    }

    /// <summary>Gets the wrapped text box.</summary>
    public TextBox Input => InputBox;

    /// <summary>Gets the suggestion currently shown after the caret, or an empty string when none is shown.</summary>
    public string Suggestion => _shown?.Result.Text ?? string.Empty;

    /// <summary>Gets the inline collection the mirror of the text is written into.</summary>
    private InlineCollection MirrorInlines =>
        GhostText.Inlines ?? throw new InvalidOperationException("The suggestion mirror has no inline collection.");

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CancelPending();
    }

    private void OnInputPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_applying || (e.Property != TextBox.TextProperty && e.Property != TextBox.CaretIndexProperty))
        {
            return;
        }

        // Only a new text and caret position is asked about. The box may report the same position again, for example when
        // its own appearance changes; that is not an edit.
        var position = new CaretBuffer(InputBox.Text ?? string.Empty, InputBox.CaretIndex);
        if (position != _lastAsked)
        {
            Restart(position);
        }
    }

    // Discards whatever was shown or pending for the old position, then asks again once the position has settled. No request
    // is sent unless a provider is configured.
    private void Restart(CaretBuffer position)
    {
        _lastAsked = position;
        CancelPending();
        ClearSuggestion();
        UnavailableText.IsVisible = false;

        if (_client is not { Settings.IsConfigured: true })
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _pending = cancellation;
        Observe(SuggestAfterDelayAsync(_client, position, cancellation.Token));
    }

    private async Task SuggestAfterDelayAsync(CompletionClient client, CaretBuffer position, CancellationToken token)
    {
        try
        {
            await Task.Delay(_debounce, token).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            var result = await client.SuggestAsync(position, token).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (result.Status == CompletionStatus.Suggested)
            {
                Show(new ShownSuggestion(position, result));
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // A newer edit or caret move replaced this request.
        }
        catch (HttpRequestException)
        {
            ShowUnavailable(token);
        }
        catch (JsonException)
        {
            ShowUnavailable(token);
        }
    }

    private void ShowUnavailable(CancellationToken token)
    {
        if (!token.IsCancellationRequested)
        {
            UnavailableText.IsVisible = true;
        }
    }

    private void Show(ShownSuggestion suggestion)
    {
        _shown = suggestion;
        _inputBrush = InputBox.Foreground;

        var muted = BlockTokens.Get<IBrush>("OmpOS.Color.TextSecondary");
        var text = suggestion.Position.Text;
        var caret = suggestion.Position.Caret;
        var inlines = MirrorInlines;

        inlines.Clear();
        AddText(inlines, text[..caret], brush: null);
        AddText(inlines, suggestion.Result.Text, muted);
        AddText(inlines, text[caret..], brush: null);

        InputBox.Foreground = Brushes.Transparent;
        GhostHost.IsVisible = true;
        AutomationProperties.SetHelpText(InputBox, ShownHelp);
    }

    private void ClearSuggestion()
    {
        if (_shown is null)
        {
            return;
        }

        _shown = null;
        MirrorInlines.Clear();
        GhostHost.IsVisible = false;
        InputBox.Foreground = _inputBrush;
        _inputBrush = null;
        AutomationProperties.SetHelpText(InputBox, string.Empty);
    }

    // Inserts the shown suggestion at the caret. The change is applied without a new request: the suggestion answered this
    // position, and the next edit asks about the new one.
    private void Accept()
    {
        if (_shown is not { } shown)
        {
            return;
        }

        var accepted = CompletionAcceptance.Accept(shown.Position, shown.Result);
        CancelPending();
        ClearSuggestion();

        _applying = true;
        try
        {
            InputBox.Text = accepted.Text;
            InputBox.CaretIndex = accepted.Caret;
        }
        finally
        {
            _applying = false;
        }

        _lastAsked = accepted;
    }

    private void Dismiss()
    {
        CancelPending();
        ClearSuggestion();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_shown is null)
        {
            return;
        }

        if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.None)
        {
            Accept();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Dismiss();
            e.Handled = true;
        }
    }

    private void CancelPending()
    {
        if (_pending is null)
        {
            return;
        }

        _pending.Cancel();
        _pending.Dispose();
        _pending = null;
    }

    // Writes the text as runs, with a line break for each new line, so the mirror wraps like the text box.
    private static void AddText(InlineCollection inlines, string text, IBrush? brush)
    {
        var lines = text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0)
            {
                inlines.Add(new LineBreak());
            }

            var line = lines[index].TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            var run = new Run(line);
            if (brush is not null)
            {
                run.Foreground = brush;
            }

            inlines.Add(run);
        }
    }

    // Surfaces a fault from background work on the UI thread, where it propagates like any other unhandled error.
    private static void Observe(Task work) => _ = work.ContinueWith(
        faulted => Dispatcher.UIThread.Post(() => ExceptionDispatchInfo.Throw(faulted.Exception!.GetBaseException())),
        CancellationToken.None,
        TaskContinuationOptions.OnlyOnFaulted,
        TaskScheduler.Default);

    private sealed record ShownSuggestion(CaretBuffer Position, CompletionResult Result);
}
