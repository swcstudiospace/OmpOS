using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Customize;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Settings;

/// <summary>
/// The Rules, Skills and Commands pages: a read-only catalogue of entries with title, scope, origin and description, the files that could
/// not be read, and the count in a polite live region. One view draws all three <see cref="CustomizeCatalogPage"/> types.
/// </summary>
internal partial class CustomizeCatalogView : SettingsPageViewBase
{
    /// <summary>Creates a view with no page.</summary>
    public CustomizeCatalogView()
    {
        InitializeComponent();
        Start();
    }

    /// <summary>Gets the live region that says how many entries the page lists.</summary>
    internal TextBlock Count => CountLine;

    /// <summary>Gets the explanation shown when the catalogue lists nothing.</summary>
    internal TextBlock EmptyText => EmptyLine;

    /// <summary>Gets the block that lists the files that could not be read.</summary>
    internal StackPanel Problems => ProblemsBlock;

    /// <inheritdoc />
    internal override PageStatusView Status => StateView;

    /// <inheritdoc />
    protected override Control ReadyHost => ReadyPanel;

    /// <inheritdoc />
    protected override void OnRefresh(ISettingsPage page, PageStatus status)
    {
        if (page is CustomizeCatalogPage catalog)
        {
            ProblemsBlock.IsVisible = catalog.Problems.Count > 0;
            EmptyLine.IsVisible = catalog.State == CustomizePageState.Empty;

            // "0 entries." and the empty sentence say the same thing. The count stays a live region but draws nothing while the page is empty.
            var drawsNothing = catalog.State == CustomizePageState.Empty;
            CountLine.Opacity = drawsNothing ? 0 : 1;
            CountLine.MaxHeight = drawsNothing ? 0 : double.PositiveInfinity;
            TableBlock.IsVisible = catalog.State == CustomizePageState.Ready;
        }
    }
}
