using Avalonia.Automation;
using Avalonia.Controls;
using OmpOS.Contracts.Ui;

namespace OmpOS.Native.Views.Blocks;

/// <summary>
/// A file edit: the path, whether the edit created or changed the file, and the text. A change shows the text before
/// the edit and the text after it, each under its own label, in monospace; a creation shows the new text alone.
/// </summary>
internal partial class FileEditBlockView : UserControl
{
    /// <summary>The operation label of an edit that created the file.</summary>
    internal const string CreatedLabel = "Created";

    /// <summary>The operation label of an edit that changed an existing file.</summary>
    internal const string EditedLabel = "Edited";

    /// <summary>Builds the view for one file edit.</summary>
    /// <param name="block">The file-edit block.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    public FileEditBlockView(FileEditBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        InitializeComponent();
        PathText.Text = block.Path;
        var operation = block.OldText is null ? CreatedLabel : EditedLabel;
        OperationText.Text = operation;
        if (block.OldText is not null)
        {
            AddSection("Before", block.OldText);
            AddSection("After", block.NewText);
        }
        else
        {
            Sections.Children.Add(BlockControls.Code(block.NewText));
        }

        AutomationProperties.SetName(this, $"File edit {block.Path}, {operation}");
    }

    private void AddSection(string label, string text)
    {
        var heading = BlockControls.Text(label, "muted");
        heading.FontSize = BlockTokens.Get<double>("OmpOS.Font.Size.Label");
        Sections.Children.Add(heading);
        Sections.Children.Add(BlockControls.Code(text));
    }
}
