using Avalonia.Controls;
using Autonogrammer.Contracts.Ui;

namespace Autonogrammer.Native.Views.Blocks;

/// <summary>A markdown block: its syntax tree drawn as text blocks and inlines. Raw HTML is drawn as text, never as markup.</summary>
internal partial class MarkdownBlockView : UserControl
{
    /// <summary>Builds the view for one markdown block.</summary>
    /// <param name="block">The markdown block.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    public MarkdownBlockView(MarkdownBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        InitializeComponent();
        foreach (var child in MarkdownAstBuilder.Build(block.Markdown))
        {
            Body.Children.Add(child);
        }
    }
}
