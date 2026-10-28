using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Native.Views.Settings;

/// <summary>
/// The base of every settings page view. It watches the <see cref="ISettingsPage"/> that is its data context while the view is attached,
/// shows its loading or failed-load state through a <see cref="PageStatusView"/> instead of the page's controls, offers Try again
/// for a failed load, and hands the page to the derived view for everything else.
/// </summary>
/// <remarks>
/// A derived view calls <see cref="Start"/> once, after <c>InitializeComponent</c>. Before that the base touches none of the named controls.
/// </remarks>
internal abstract class SettingsPageViewBase : UserControl
{
    private readonly ModelWatcher<INotifyPropertyChanged> _watch;
    private bool _attached;
    private bool _started;

    /// <summary>Creates a view with no page.</summary>
    protected SettingsPageViewBase()
    {
        _watch = new ModelWatcher<INotifyPropertyChanged>(Refresh);
    }

    /// <summary>Gets the page the view draws, or null.</summary>
    internal ISettingsPage? Page => DataContext as ISettingsPage;

    /// <summary>Gets the state view of the page: loading, failed load and empty.</summary>
    internal abstract PageStatusView Status { get; }

    /// <summary>Gets the control that holds the page's controls. It is hidden while the page shows a state instead.</summary>
    protected abstract Control ReadyHost { get; }

    /// <summary>Draws the current state. Call it after the page changed in a way the page's own notifications do not announce.</summary>
    internal void Refresh()
    {
        if (!_started)
        {
            return;
        }

        if (Page is not { } page)
        {
            Status.Clear();
            ReadyHost.IsVisible = false;
            return;
        }

        var status = SettingsPageViews.StatusOf(page);
        switch (status.Kind)
        {
            case PageStatusKind.Loading:
                Status.ShowLoading(status.Message);
                break;
            case PageStatusKind.Error:
                Status.ShowError(status.Heading, status.Message);
                break;
            default:
                Status.Clear();
                break;
        }

        ReadyHost.IsVisible = status.Kind == PageStatusKind.Ready;
        OnRefresh(page, status);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _attached = true;
        Bind();
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _attached = false;
        _watch.Set(null);
        OnPageChanged(null);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_attached)
        {
            Bind();
        }

        Refresh();
    }

    /// <summary>Connects the Try again button and draws the first state. Call it once, after <c>InitializeComponent</c>.</summary>
    protected void Start()
    {
        Status.RetryRequested += OnRetryRequested;
        _started = true;
        Refresh();
    }

    /// <summary>Called when the page the view draws changes, and with null when the view is detached. Do nothing by default.</summary>
    /// <param name="page">The new page, or null.</param>
    protected virtual void OnPageChanged(ISettingsPage? page)
    {
    }

    /// <summary>Called after every redraw of the state, with the page and what it shows. Do nothing by default.</summary>
    /// <param name="page">The page.</param>
    /// <param name="status">What the page shows.</param>
    protected virtual void OnRefresh(ISettingsPage page, PageStatus status)
    {
    }

    private void Bind()
    {
        _watch.Set(Page);
        OnPageChanged(Page);
    }

    private void OnRetryRequested(object? sender, EventArgs e)
    {
        if (Page is { } page)
        {
            // A load reports its expected failures in the page's state and never throws them, so the task is not observed here.
            _ = page.LoadAsync(CancellationToken.None);
        }
    }
}
