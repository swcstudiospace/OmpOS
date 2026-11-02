using System.Globalization;
using Autonogrammer.Presentation.Cowork.Artifacts;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Autonogrammer.Native.Views.Cowork.Artifacts;

/// <summary>
/// The Access review for one version (D-26, D-27). It lists each requested host in ASCII (punycode) form with the decoded form
/// and a mixed-script mark, then offers Allow for this session, Allow for this version and Deny. Deny has focus, and Escape
/// chooses Deny. The first answer is final; a later click or key raises nothing.
/// </summary>
public partial class ArtifactAccessReviewView : UserControl
{
    private ArtifactAccessReviewModel? _review;

    /// <summary>Creates the view.</summary>
    public ArtifactAccessReviewView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bind(DataContext as ArtifactAccessReviewModel);
        AllowSessionButton.Click += (_, _) => Choose(ArtifactAccessChoice.AllowSession);
        AllowVersionButton.Click += (_, _) => Choose(ArtifactAccessChoice.AllowVersion);
        DenyButton.Click += (_, _) => Choose(ArtifactAccessChoice.Deny);
        KeyDown += OnKeyDown;
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => DenyButton.Focus());
    }

    /// <summary>Occurs once, when the first answer is recorded.</summary>
    public event EventHandler<ArtifactAccessDecisionEventArgs>? DecisionMade;

    /// <summary>Gets the Allow for this session button.</summary>
    public Button SessionChoice => AllowSessionButton;

    /// <summary>Gets the Allow for this version button.</summary>
    public Button VersionChoice => AllowVersionButton;

    /// <summary>Gets the Deny button, which has focus when the review opens.</summary>
    public Button DenyChoice => DenyButton;

    /// <summary>Gets the list of requested hosts.</summary>
    public StackPanel Origins => OriginList;

    /// <summary>Gets the deadline sentence.</summary>
    public string DeadlineSentence => DeadlineText.Text ?? string.Empty;

    /// <summary>Returns the sentence that lists one host: its ASCII form, the decoded form when it differs, and the mixed-script warning.</summary>
    /// <param name="origin">The host.</param>
    /// <returns>The sentence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="origin"/> is null.</exception>
    public static string OriginSentence(ArtifactOriginView origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        var sentence = "Origin: " + origin.Ascii;
        if (!string.Equals(origin.Ascii, origin.Decoded, StringComparison.Ordinal))
        {
            sentence += ". Shown as: " + origin.Decoded;
        }

        if (origin.MixedScript)
        {
            sentence += ". Mixed scripts: check this host before you allow it.";
        }

        return sentence;
    }

    private void Bind(ArtifactAccessReviewModel? review)
    {
        _review = review;
        OriginList.Children.Clear();
        if (review is null)
        {
            return;
        }

        Banner.Text = ArtifactAccessReviewModel.BannerText;
        foreach (var origin in review.Origins)
        {
            OriginList.Children.Add(new TextBlock { Text = OriginSentence(origin), TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        }

        ReasonText.Text = ArtifactAccessReviewModel.ReasonLabel;
        WarningText.Text = ArtifactAccessReviewModel.WarningText;
        DeadlineText.Text = "Denied automatically at " + review.Deadline.ToString("u", CultureInfo.InvariantCulture) + " if you do not answer.";
    }

    private void Choose(ArtifactAccessChoice choice)
    {
        if (_review is null || _review.Decision is not null)
        {
            return;
        }

        DecisionMade?.Invoke(this, new ArtifactAccessDecisionEventArgs(_review.Choose(choice)));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _review is null || _review.Decision is not null)
        {
            return;
        }

        e.Handled = true;
        DecisionMade?.Invoke(this, new ArtifactAccessDecisionEventArgs(_review.Escape()));
    }
}
