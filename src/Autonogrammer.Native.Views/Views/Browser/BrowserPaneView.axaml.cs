using System.ComponentModel;
using Autonogrammer.Contracts.Cowork.Browser;
using Autonogrammer.Presentation.Cowork.Browser;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace Autonogrammer.Native.Views.Browser;

/// <summary>
/// The desktop browser pane. It binds <see cref="BrowserPaneViewModel"/> only. Stop and Close stay in the chrome, outside the page frame.
/// </summary>
public partial class BrowserPaneView : UserControl
{
    private BrowserPaneViewModel? _model;
    private bool _keyboardCaptured;

    /// <summary>Creates the pane with no model.</summary>
    public BrowserPaneView()
    {
        InitializeComponent();
        TakeOverButton.Click += OnTakeOver;
        StopButton.Click += OnStop;
        CloseButton.Click += OnClose;
        FixButton.Click += OnFix;
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        PageFrame.PointerPressed += OnPointerPressed;
    }

    /// <summary>Gets the page frame. Stop is not inside it.</summary>
    public Border Frame => PageFrame;

    /// <summary>Gets the frame image.</summary>
    public Image FramePicture => FrameImage;

    /// <summary>Gets the chrome Stop button.</summary>
    public Button Stop => StopButton;

    /// <summary>Gets the chrome Close button.</summary>
    public Button CloseBrowser => CloseButton;

    /// <summary>Gets the Take over button.</summary>
    public Button TakeOver => TakeOverButton;

    /// <summary>Gets the chrome sentence. It is outside the page frame.</summary>
    public TextBlock HeaderText => HeaderSentence;

    /// <summary>Gets the state sentence.</summary>
    public TextBlock SentenceText => Sentence;

    /// <summary>Gets the polite announcement.</summary>
    public TextBlock AnnouncementText => Announcement;

    /// <summary>Gets the fix action, including Choose a vision model.</summary>
    public Button Fix => FixButton;

    /// <summary>Gets the vision-model choices.</summary>
    public ItemsControl Vision => VisionChoices;

    /// <summary>Gets the Agent view strip.</summary>
    public TextBlock AgentView => AgentStrip;

    /// <summary>Gets whether this pane is holding keyboard capture. F6 releases it.</summary>
    public bool KeyboardCaptured => _keyboardCaptured;

    /// <summary>Builds alt text that carries the page title and the host. A session id <c>title|host</c> supplies both; otherwise the title is Page and the host is the session id.</summary>
    /// <param name="frame">The frame.</param>
    /// <returns>The alt text.</returns>
    public static string FrameAltText(BrowserFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var split = frame.SessionId.Split('|', 2);
        var title = split.Length == 2 && split[0].Length > 0 ? split[0] : "Page";
        var host = split.Length == 2 ? split[1] : frame.SessionId;
        return title + " at " + host;
    }

    /// <summary>Binds the pane to the view model.</summary>
    /// <param name="model">The pane model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public void Attach(BrowserPaneViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
        }

        _model = model;
        model.PropertyChanged += OnModelChanged;
        Apply();
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e) => Apply();

    private void Apply()
    {
        if (_model is null)
        {
            return;
        }

        Sentence.Text = _model.Sentence;
        AutomationProperties.SetName(Sentence, _model.Sentence);
        HeaderSentence.Text = _model.HeaderSentence;
        HeaderSentence.IsVisible = _model.HeaderSentence.Length > 0;
        StopButton.IsVisible = _model.HeaderOffersStop;
        CloseButton.IsVisible = _model.HeaderOffersClose;
        FixButton.Content = _model.FixAction;
        FixButton.IsVisible = _model.FixAction.Length > 0;
        AutomationProperties.SetName(FixButton, _model.FixAction);
        Announcement.Text = _model.Announcement ?? string.Empty;
        var showVision = _model.State == BrowserPaneState.ModelCannotSeeImages;
        VisionChoices.IsVisible = showVision;
        VisionChoices.ItemsSource = showVision ? _model.VisionSelectors : null;
        var hasImage = _model.LastImageByteCount > 0;
        AgentStrip.IsVisible = hasImage;
        AgentStrip.Text = hasImage
            ? $"Agent view {_model.LastImageFormat} {_model.LastImageWidth}x{_model.LastImageHeight} {_model.LastImageByteCount} bytes {_model.LastImageTime:O}"
            : string.Empty;
        ApplyFrame();
    }

    private void ApplyFrame()
    {
        var frame = _model?.LatestFrame;
        var show = frame is not null && _model!.State != BrowserPaneState.RemoteViewUnavailable;
        FrameImage.IsVisible = show;
        if (!show || frame is null)
        {
            FrameImage.Source = null;
            AutomationProperties.SetName(FrameImage, null);
            return;
        }

        var alt = FrameAltText(frame);
        AutomationProperties.SetName(FrameImage, alt);
        FrameImage.Source = TryDecode(frame.Jpeg.Span);
    }

    private static Bitmap? TryDecode(ReadOnlySpan<byte> jpeg)
    {
        if (jpeg.Length < 3 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(jpeg.ToArray());
            return new Bitmap(stream);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private void OnTakeOver(object? sender, RoutedEventArgs e)
    {
        _keyboardCaptured = true;
        _ = _model?.TakeOverAsync();
    }

    private void OnStop(object? sender, RoutedEventArgs e)
    {
        _keyboardCaptured = false;
        _ = _model?.StopAsync();
    }

    private void OnClose(object? sender, RoutedEventArgs e) => _ = _model?.CloseBrowserAsync();

    private void OnFix(object? sender, RoutedEventArgs e)
    {
        if (_model?.State == BrowserPaneState.ModelCannotSeeImages)
        {
            VisionChoices.IsVisible = true;
            VisionChoices.ItemsSource = _model.VisionSelectors;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_model is null)
        {
            return;
        }

        if (e.Key == Key.F6 && _keyboardCaptured)
        {
            e.Handled = true;
            _keyboardCaptured = false;
            _ = _model.ReleaseControlAsync();
            return;
        }

        if (!_keyboardCaptured)
        {
            if (e.KeyModifiers == KeyModifiers.Alt && e.Key == Key.T)
            {
                e.Handled = true;
                OnTakeOver(this, e);
            }
            else if (e.KeyModifiers == KeyModifiers.Alt && e.Key == Key.S && StopButton.IsVisible)
            {
                e.Handled = true;
                OnStop(this, e);
            }
            else if (e.KeyModifiers == KeyModifiers.Alt && e.Key == Key.C && CloseButton.IsVisible)
            {
                e.Handled = true;
                OnClose(this, e);
            }

            return;
        }

        if (e.Key == Key.F6)
        {
            return;
        }

        e.Handled = true;
        _ = _model.SendKeyAsync(new BrowserKeyInput(e.Key.ToString(), e.KeySymbol, "keyDown"));
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_model is null || !_keyboardCaptured)
        {
            return;
        }

        var point = e.GetPosition(PageFrame);
        var width = Math.Max(1, (int)PageFrame.Bounds.Width);
        var height = Math.Max(1, (int)PageFrame.Bounds.Height);
        _ = _model.SendPointerAsync(new BrowserPointerInput(point.X, point.Y, width, height, "mousePressed", "left"));
    }
}
