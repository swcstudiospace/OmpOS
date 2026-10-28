using Avalonia.Automation;
using Avalonia.Controls;
using Autonogrammer.Contracts.Ui;

namespace Autonogrammer.Native.Views.Blocks;

/// <summary>An unknown block: a collapsed disclosure that names the kind and shows the raw JSON when opened.</summary>
internal partial class UnknownBlockView : UserControl
{
    /// <summary>Builds the collapsed disclosure for one unknown block.</summary>
    /// <param name="block">The unknown block.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    public UnknownBlockView(UnknownBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        InitializeComponent();
        var header = block.OriginalKind is null ? "Unsupported content" : $"Unsupported content ({block.OriginalKind})";
        Disclosure.Header = header;
        AutomationProperties.SetName(Disclosure, header);
        Disclosure.Content = BlockControls.Code(block.Raw.GetRawText());
    }
}
