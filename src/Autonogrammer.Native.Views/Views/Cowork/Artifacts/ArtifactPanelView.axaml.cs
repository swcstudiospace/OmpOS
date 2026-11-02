using System.ComponentModel;
using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Presentation.Cowork.Artifacts;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace Autonogrammer.Native.Views.Cowork.Artifacts;

/// <summary>
/// The artifact panel (D-36). It shows the labelled region, the status line, the Access banner, the toolbar, the polite live
/// region, the diff, and the generated content inside a frame titled "Generated content: title (kind), sandboxed". Surface kinds
/// (Html, Svg, Mermaid) load through the attached <see cref="NativeWebViewArtifactSurface"/>; when the surface is unavailable
/// the source is shown with the reason. Toolbar, Access and Return commands are raised as events; the host runs them against
/// the models, so the view holds no state the models do not hold.
/// </summary>
public partial class ArtifactPanelView : UserControl
{
    private ArtifactPanelModel? _model;
    private PropertyChangedEventHandler? _modelChanged;
    private ArtifactContent? _content;
    private NativeWebViewArtifactSurface? _surface;

    /// <summary>Creates the view.</summary>
    public ArtifactPanelView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bind(DataContext as ArtifactPanelModel);
        DiffButton.Click += (_, _) => Raise(ArtifactCommand.Diff);
        CopyButton.Click += (_, _) => Raise(ArtifactCommand.Copy);
        ExportButton.Click += (_, _) => Raise(ArtifactCommand.Export);
        EditButton.Click += (_, _) => Raise(ArtifactCommand.Edit);
        AccessButton.Click += (_, _) => Raise(ArtifactCommand.Access);
        StopButton.Click += (_, _) => Raise(ArtifactCommand.Stop);
        CloseButton.Click += (_, _) => Raise(ArtifactCommand.Close);
        ReviewAccessButton.Click += (_, _) => Raise(ArtifactCommand.Access);
        SkipLink.Click += (_, _) => ReturnButton.Focus();
        ReturnButton.Click += (_, _) => ReturnToChatRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Occurs when a toolbar or banner command is chosen.</summary>
    public event EventHandler<ArtifactCommandEventArgs>? CommandRequested;

    /// <summary>Occurs when the user returns focus to the chat.</summary>
    public event EventHandler? ReturnToChatRequested;

    /// <summary>Occurs when the user answers the Access review shown by <see cref="ShowAccessReview"/>.</summary>
    public event EventHandler<ArtifactAccessDecisionEventArgs>? AccessDecisionMade;

    /// <summary>Gets the labelled heading control.</summary>
    public TextBlock HeadingLabel => HeadingText;

    /// <summary>Gets the polite live-region control.</summary>
    public TextBlock AnnouncementText => Announcement;

    /// <summary>Gets the Skip generated content link. It sits before the frame.</summary>
    public Button SkipGeneratedContentLink => SkipLink;

    /// <summary>Gets the Return to chat button. It sits after the frame.</summary>
    public Button ReturnToChatButton => ReturnButton;

    /// <summary>Gets the frame that holds the generated content.</summary>
    public Border FrameArea => FrameBorder;

    /// <summary>Gets the content host inside the frame.</summary>
    public ContentControl FrameContentHost => FrameContent;

    /// <summary>Gets the diff panel.</summary>
    public StackPanel DiffPanelControl => DiffPanel;

    /// <summary>Gets the Access review host.</summary>
    public ContentControl AccessReviewHost => AccessHost;

    /// <summary>Gets the toolbar.</summary>
    public WrapPanel Toolbar => ToolbarPanel;

    /// <summary>Gets the Access banner.</summary>
    public Border AccessBanner => Banner;

    /// <summary>Attaches the desktop surface that loads surface kinds into the frame.</summary>
    /// <param name="surface">The surface. The same instance must be the panel model's surface.</param>
    /// <exception cref="ArgumentNullException"><paramref name="surface"/> is null.</exception>
    public void AttachSurface(NativeWebViewArtifactSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _surface = surface;
        Refresh();
    }

    /// <summary>Sets the content of the shown version. The host reads the bytes from the store.</summary>
    /// <param name="content">The stored version.</param>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    public void ShowContent(ArtifactContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        _content = content;
        Refresh();
    }

    /// <summary>Shows the diff lines. Every line carries a sign and a word as well as its text.</summary>
    /// <param name="diff">The diff.</param>
    /// <exception cref="ArgumentNullException"><paramref name="diff"/> is null.</exception>
    public void ShowDiff(ArtifactDiffModel diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        DiffPanel.Children.Clear();
        DiffPanel.Children.Add(new TextBlock { Text = diff.Summary, TextWrapping = TextWrapping.Wrap });
        foreach (var line in diff.Lines)
        {
            var sentence = FormattableString.Invariant($"{ArtifactDiffModel.MarkerFor(line.Change)} {ArtifactDiffModel.LabelFor(line.Change)}: {line.Text}");
            DiffPanel.Children.Add(new TextBlock { Text = sentence, TextWrapping = TextWrapping.Wrap });
        }

        DiffPanel.IsVisible = true;
    }

    /// <summary>Hides the diff.</summary>
    public void HideDiff()
    {
        DiffPanel.Children.Clear();
        DiffPanel.IsVisible = false;
    }

    /// <summary>Shows the Access review for a version. The review's answer is raised as <see cref="AccessDecisionMade"/>.</summary>
    /// <param name="review">The review.</param>
    /// <exception cref="ArgumentNullException"><paramref name="review"/> is null.</exception>
    public void ShowAccessReview(ArtifactAccessReviewModel review)
    {
        ArgumentNullException.ThrowIfNull(review);
        HideAccessReview();
        var view = new ArtifactAccessReviewView { DataContext = review };
        view.DecisionMade += OnDecisionMade;
        AccessHost.Content = view;
    }

    /// <summary>Removes the Access review without an answer.</summary>
    public void HideAccessReview()
    {
        if (AccessHost.Content is ArtifactAccessReviewView view)
        {
            view.DecisionMade -= OnDecisionMade;
        }

        AccessHost.Content = null;
    }

    /// <summary>Returns the button of one toolbar command.</summary>
    /// <param name="command">The command.</param>
    /// <returns>The button.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="command"/> is not a defined command.</exception>
    public Button CommandButton(ArtifactCommand command) => command switch
    {
        ArtifactCommand.Diff => DiffButton,
        ArtifactCommand.Copy => CopyButton,
        ArtifactCommand.Export => ExportButton,
        ArtifactCommand.Edit => EditButton,
        ArtifactCommand.Access => AccessButton,
        ArtifactCommand.Stop => StopButton,
        ArtifactCommand.Close => CloseButton,
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "No toolbar button is defined for this command."),
    };

    /// <summary>Returns the accessible name of the frame for one version: the title and kind, and that it is sandboxed.</summary>
    /// <param name="item">The version.</param>
    /// <returns>The name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public static string FrameName(ArtifactPanelItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return FormattableString.Invariant($"Generated content: {item.Title} ({KindName(item.Kind)}), sandboxed");
    }

    private static bool IsSurfaceKind(ArtifactKind kind) =>
        kind is ArtifactKind.Html or ArtifactKind.Svg or ArtifactKind.Mermaid;

    private static string KindName(ArtifactKind kind) => kind switch
    {
        ArtifactKind.Markdown => "markdown",
        ArtifactKind.Code => "code",
        ArtifactKind.Html => "html",
        ArtifactKind.Svg => "svg",
        ArtifactKind.Mermaid => "mermaid",
        ArtifactKind.Csv => "csv",
        ArtifactKind.Image => "image",
        _ => FormattableString.Invariant($"kind {(int)kind}"),
    };

    private static string ReasonSentence(ArtifactSurfaceReason reason) => reason switch
    {
        ArtifactSurfaceReason.MermaidAssetMissing => "The Mermaid bundle is not available, so the source is shown.",
        ArtifactSurfaceReason.SurfaceUnavailable => "The web view cannot be created on this host, so the source is shown.",
        ArtifactSurfaceReason.DocumentRefused => "The document policy refused this version, so the source is shown.",
        _ => "The source is shown.",
    };

    private void Bind(ArtifactPanelModel? model)
    {
        if (_model is not null && _modelChanged is not null)
        {
            _model.PropertyChanged -= _modelChanged;
        }

        _model = model;
        _modelChanged = null;
        if (model is not null)
        {
            _modelChanged = (_, _) => Refresh();
            model.PropertyChanged += _modelChanged;
        }

        Refresh();
    }

    private void Raise(ArtifactCommand command) => CommandRequested?.Invoke(this, new ArtifactCommandEventArgs(command));

    private void OnDecisionMade(object? sender, ArtifactAccessDecisionEventArgs e)
    {
        HideAccessReview();
        AccessDecisionMade?.Invoke(this, e);
    }

    private void Refresh()
    {
        if (_model is null)
        {
            IsVisible = false;
            return;
        }

        IsVisible = _model.IsOpen;
        AutomationProperties.SetName(this, _model.Heading);
        ChromeLabel.Text = ArtifactPanelModel.ChromeLabel;
        HeadingText.Text = _model.Heading;
        StatusText.Text = _model.StatusLine;
        Banner.IsVisible = _model.ReviewAccessAvailable;
        BannerText.Text = ArtifactAccessReviewModel.BannerText;
        Announcement.Text = _model.Announcement;
        ReturnButton.IsVisible = _model.IsOpen;
        ApplyToolbar(_model);
        ApplyFrame(_model);
    }

    private void ApplyToolbar(ArtifactPanelModel model)
    {
        foreach (var command in Enum.GetValues<ArtifactCommand>())
        {
            CommandButton(command).IsEnabled = false;
        }

        CloseButton.IsEnabled = true;
        var item = model.Current;
        if (item is null)
        {
            return;
        }

        if (!Enum.IsDefined(item.Kind))
        {
            ExportButton.IsEnabled = true;
            return;
        }

        var surfaceActive = model.SurfaceState is ArtifactSurfaceState.Loading or ArtifactSurfaceState.Running or ArtifactSurfaceState.NavigationBlocked;
        var row = new ArtifactListItem(item.ArtifactId, item.Title, item.Kind, item.VersionCount, item.RequestedOrigins);
        foreach (var state in ArtifactListModel.CommandsFor(row, surfaceActive))
        {
            CommandButton(state.Command).IsEnabled = state.IsEnabled;
        }
    }

    private void ApplyFrame(ArtifactPanelModel model)
    {
        var item = model.Current;
        SkipLink.IsVisible = item is not null;
        if (item is null)
        {
            FrameBorder.IsVisible = false;
            FrameContent.Content = null;
            return;
        }

        FrameContent.Content = ContentFor(item, model);
        FrameBorder.IsVisible = true;
        AutomationProperties.SetName(FrameBorder, FrameName(item));
    }

    private object ContentFor(ArtifactPanelItem item, ArtifactPanelModel model)
    {
        if (!Enum.IsDefined(item.Kind))
        {
            return Metadata(item);
        }

        if (IsSurfaceKind(item.Kind))
        {
            if (_surface is not null && model.SurfaceState != ArtifactSurfaceState.Unavailable)
            {
                return _surface.View;
            }

            var reason = _surface is null ? ArtifactSurfaceReason.SurfaceUnavailable : model.SurfaceReason;
            return Source(reason, _content);
        }

        return _content is null ? Message("No content is loaded for this version.") : ViewFor(_content);
    }

    private static Control ViewFor(ArtifactContent content)
    {
        switch (content.Kind)
        {
            case ArtifactKind.Csv:
            {
                var table = new ArtifactTableView();
                table.Show(content.Title, content.Bytes);
                return table;
            }

            case ArtifactKind.Image:
            {
                var image = new ArtifactImageView();
                image.Show(content.Title, content.MediaType, content.Bytes);
                return image;
            }

            case ArtifactKind.Markdown:
            {
                var text = ArtifactDocumentBuilder.Decode(content.Bytes);
                if (text is null)
                {
                    return Message("This version is not valid UTF-8 text.");
                }

                var markdown = new ArtifactMarkdownView();
                markdown.Show(text);
                return markdown;
            }

            case ArtifactKind.Code:
            {
                var text = ArtifactDocumentBuilder.Decode(content.Bytes);
                if (text is null)
                {
                    return Message("This version is not valid UTF-8 text.");
                }

                var code = new ArtifactCodeView();
                code.Show(content.Language, text);
                return code;
            }

            default:
                throw new InvalidOperationException("Surface kinds are shown through the surface, not a content view.");
        }
    }

    private static StackPanel Source(ArtifactSurfaceReason reason, ArtifactContent? content)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = ReasonSentence(reason), TextWrapping = TextWrapping.Wrap });
        var code = new ArtifactCodeView();
        var text = content is null ? null : ArtifactDocumentBuilder.Decode(content.Bytes);
        code.Show(null, text ?? string.Empty);
        panel.Children.Add(code);
        return panel;
    }

    private static StackPanel Metadata(ArtifactPanelItem item)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = item.Title, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold });
        panel.Children.Add(new TextBlock
        {
            Text = FormattableString.Invariant($"Kind: {KindName(item.Kind)}"),
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(new TextBlock
        {
            Text = "This version of Autonogrammer cannot display this kind. Export the version to keep it.",
            TextWrapping = TextWrapping.Wrap,
        });
        return panel;
    }

    private static TextBlock Message(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
}
