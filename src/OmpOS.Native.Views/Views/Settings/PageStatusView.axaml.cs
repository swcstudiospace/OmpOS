using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OmpOS.Native.Views.Settings;

/// <summary>
/// The state of a settings page that is not the page's controls: loading, or a failed load with a Try again button.
/// A page view shows one of them, or hides both while it shows its controls. The view holds no model: the page view
/// tells it what to say, and handles <see cref="RetryRequested"/>.
/// </summary>
internal partial class PageStatusView : UserControl
{
    /// <summary>Creates a view that shows nothing.</summary>
    public PageStatusView()
    {
        InitializeComponent();
    }

    /// <summary>Raised when the user asks to load the page again.</summary>
    public event EventHandler? RetryRequested;

    /// <summary>Gets the Try again button, for the tests.</summary>
    internal Button Retry => RetryButton;

    /// <summary>Gets a value indicating whether the loading text is showing.</summary>
    internal bool IsLoadingShown => LoadingText.IsVisible;

    /// <summary>Gets a value indicating whether the error block is showing.</summary>
    internal bool IsErrorShown => ErrorBlock.IsVisible;

    /// <summary>Gets the text of the error body, for the tests.</summary>
    internal string? ErrorMessage => ErrorBody.Text;

    /// <summary>Gets the text of the loading line, for the tests.</summary>
    internal string? LoadingMessage => LoadingText.Text;

    /// <summary>Shows the loading text and nothing else.</summary>
    /// <param name="text">The loading text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    internal void ShowLoading(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        LoadingText.Text = text;
        AutomationProperties.SetName(LoadingText, text);
        LoadingText.IsVisible = true;
        ErrorBlock.IsVisible = false;
    }

    /// <summary>Shows the failed-load heading, the reason and the Try again button.</summary>
    /// <param name="heading">The heading.</param>
    /// <param name="message">The reason the page failed to load.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    internal void ShowError(string heading, string message)
    {
        ArgumentNullException.ThrowIfNull(heading);
        ArgumentNullException.ThrowIfNull(message);

        ErrorHeading.Text = heading;
        ErrorBody.Text = message;
        AutomationProperties.SetName(ErrorBlock, heading);
        ErrorBlock.IsVisible = true;
        LoadingText.IsVisible = false;
    }

    /// <summary>Hides every state.</summary>
    internal void Clear()
    {
        LoadingText.IsVisible = false;
        ErrorBlock.IsVisible = false;
    }

    private void OnRetryClick(object? sender, RoutedEventArgs e) => RetryRequested?.Invoke(this, EventArgs.Empty);
}
