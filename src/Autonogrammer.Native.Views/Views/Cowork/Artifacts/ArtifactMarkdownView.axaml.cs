using Autonogrammer.Native.Views.Blocks;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Cowork.Artifacts;

/// <summary>
/// A Markdown artifact drawn through the shared <see cref="MarkdownAstBuilder"/> pipeline, which has raw HTML disabled. No link
/// is an anchor: a relative, <c>artifact:</c> or <c>javascript:</c> target is plain text, and every other link shows its text
/// with its full URL. Nothing in this view navigates, so no link opens without a deliberate action elsewhere.
/// </summary>
public partial class ArtifactMarkdownView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ArtifactMarkdownView()
    {
        InitializeComponent();
    }

    /// <summary>Gets the number of top-level controls the markdown produced.</summary>
    public int BlockCount => Blocks.Children.Count;

    /// <summary>Renders markdown, replacing what was shown.</summary>
    /// <param name="markdown">The markdown source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="markdown"/> is null.</exception>
    public void Show(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        Blocks.Children.Clear();
        foreach (var block in MarkdownAstBuilder.Build(markdown))
        {
            Blocks.Children.Add(block);
        }
    }
}
