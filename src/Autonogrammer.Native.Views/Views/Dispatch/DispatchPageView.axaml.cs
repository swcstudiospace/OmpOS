using Autonogrammer.Presentation.Dispatch;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Dispatch;

/// <summary>
/// The Dispatch page. It binds <see cref="DispatchPageModel"/> only: the devices or their one phase, the command rows, and this desktop's
/// pairing when the head has one. Without a local device it says to pair in the desktop app.
/// </summary>
internal partial class DispatchPageView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="DispatchPageView"/> class.</summary>
    public DispatchPageView()
    {
        InitializeComponent();
        PageHeading.Text = DispatchCopy.PageHeading;
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        PairText.Text = DataContext is DispatchPageModel { PairThisDevice: { } pair }
            ? pair.StatusText
            : DispatchCopy.PairInDesktopApp;
    }
}
