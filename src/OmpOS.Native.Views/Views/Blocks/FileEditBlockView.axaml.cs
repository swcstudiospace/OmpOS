using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using OmpOS.Contracts.Ui;

namespace OmpOS.Native.Views.Blocks;

/// <summary>
/// A file edit drawn as a compact diff card: the path, the added and removed line counts, whether the edit created or changed the
/// file, and the lines. A change shows the lines of the old text and the new text as one diff, each line with its number, a sign and
/// a tint; a creation shows the new text with every line added. The card shows its first
/// <see cref="CollapsedLines"/> lines; a row under them opens the rest.
/// </summary>
internal partial class FileEditBlockView : UserControl
{
    /// <summary>The operation label of an edit that created the file.</summary>
    internal const string CreatedLabel = "Created";

    /// <summary>The operation label of an edit that changed an existing file.</summary>
    internal const string EditedLabel = "Edited";

    /// <summary>How many lines the card shows before the rest is folded behind its row.</summary>
    internal const int CollapsedLines = 8;

    private readonly List<Border> _lines = [];
    private bool _expanded;

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

        var entries = LineDiff.Compute(block.OldText, block.NewText);
        var added = entries.Count(static entry => entry.Kind == DiffEntryKind.Added);
        var removed = entries.Count(static entry => entry.Kind == DiffEntryKind.Removed);
        AddedCount.Text = BlockText.AddedCount(added);
        AddedCount.IsVisible = added > 0;
        RemovedCount.Text = BlockText.RemovedCount(removed);
        RemovedCount.IsVisible = removed > 0;

        foreach (var entry in entries)
        {
            var line = LineRow(entry);
            _lines.Add(line);
            Sections.Children.Add(line);
        }

        MoreButton.Click += (_, _) => Toggle();
        Refresh();

        AutomationProperties.SetName(this, $"File edit {block.Path}, {operation}");
    }

    private static Border LineRow(DiffEntry entry)
    {
        var number = new TextBlock
        {
            Text = entry.Number.ToString(CultureInfo.InvariantCulture),
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 0, 8, 0),
        };
        number.Classes.Add("muted");
        BlockControls.AsMono(number);

        var sign = new TextBlock
        {
            Text = entry.Kind switch
            {
                DiffEntryKind.Added => BlockText.AddedSign,
                DiffEntryKind.Removed => BlockText.RemovedSign,
                _ => string.Empty,
            },
            TextAlignment = TextAlignment.Center,
        };
        BlockControls.AsMono(sign);
        if (entry.Kind == DiffEntryKind.Added)
        {
            sign.Classes.Add("diff-added");
        }
        else if (entry.Kind == DiffEntryKind.Removed)
        {
            sign.Classes.Add("diff-removed");
        }

        var text = new TextBlock { Text = entry.Text, TextWrapping = TextWrapping.NoWrap };
        BlockControls.AsMono(text);

        Grid.SetColumn(number, 0);
        Grid.SetColumn(sign, 1);
        Grid.SetColumn(text, 2);
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("40,16,*"),
            Children = { number, sign, text },
        };

        var row = new Border { Child = grid };
        row.Classes.Add("diff-line");
        if (entry.Kind == DiffEntryKind.Added)
        {
            row.Classes.Add("added");
        }
        else if (entry.Kind == DiffEntryKind.Removed)
        {
            row.Classes.Add("removed");
        }

        return row;
    }

    private void Toggle()
    {
        _expanded = !_expanded;
        Refresh();
    }

    /// <summary>Shows the first lines, or all of them when expanded, and words the row under them to match.</summary>
    private void Refresh()
    {
        var folds = _lines.Count > CollapsedLines;
        for (var index = 0; index < _lines.Count; index++)
        {
            _lines[index].IsVisible = _expanded || index < CollapsedLines;
        }

        MoreButton.IsVisible = folds;
        if (!folds)
        {
            return;
        }

        var label = _expanded ? BlockText.ShowFewerLines : BlockText.ShowMoreLines(_lines.Count - CollapsedLines);
        MoreButton.Content = label;
        AutomationProperties.SetName(MoreButton, label);
    }
}
