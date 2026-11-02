using Autonogrammer.Cowork.Artifacts.Rendering;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Autonogrammer.Native.Views.Cowork.Artifacts;

/// <summary>
/// A CSV artifact as a read-only table. The caption carries the title with the data row and column counts. The first row is
/// the column header row. A formula-like cell shows a text mark, so the mark reads without colour. The preview is capped by
/// <see cref="CsvReader"/>, and the notice says when only the first rows are shown. Each column is a template column whose
/// cell reads its own index, so no reflection binding is used.
/// </summary>
public partial class ArtifactTableView : UserControl
{
    /// <summary>The text that marks a formula-like cell.</summary>
    public const string FormulaMark = "[formula] ";

    /// <summary>Creates the view.</summary>
    public ArtifactTableView()
    {
        InitializeComponent();
    }

    /// <summary>Gets the table.</summary>
    public DataGrid Table => Grid;

    /// <summary>Gets the caption text.</summary>
    public string CaptionText => Caption.Text ?? string.Empty;

    /// <summary>Gets the notice text, or an empty string when every row fits.</summary>
    public string NoticeText => Notice.IsVisible ? Notice.Text ?? string.Empty : string.Empty;

    /// <summary>Gets the number of data rows, the header row excluded, including rows past the preview window.</summary>
    public int DataRowCount { get; private set; }

    /// <summary>Gets the number of columns.</summary>
    public int ColumnCount { get; private set; }

    /// <summary>Reads the stored CSV bytes and shows them. A refused file shows only its caption and the refusal code.</summary>
    /// <param name="title">The validated title.</param>
    /// <param name="bytes">The stored CSV bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> is null.</exception>
    public void Show(string title, ReadOnlyMemory<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(title);
        var result = CsvReader.Read(bytes.Span);
        if (!result.IsSuccess)
        {
            Clear();
            Caption.Text = FormattableString.Invariant($"{title}: this table cannot be shown ({result.Code}).");
            return;
        }

        var rows = result.Rows;
        var columns = rows.Count == 0 ? 0 : rows.Max(static row => row.Count);
        IReadOnlyList<CsvReader.CsvCell> header = rows.Count > 0 ? rows[0] : [];
        var data = new List<string[]>(Math.Max(0, rows.Count - 1));
        for (var index = 1; index < rows.Count; index++)
        {
            var cells = new string[columns];
            for (var column = 0; column < columns; column++)
            {
                cells[column] = column < rows[index].Count ? DisplayText(rows[index][column]) : string.Empty;
            }

            data.Add(cells);
        }

        DataRowCount = data.Count + result.RemainderRowCount;
        ColumnCount = columns;
        Grid.Columns.Clear();
        for (var column = 0; column < columns; column++)
        {
            Grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = column < header.Count && header[column].Text.Length > 0
                    ? DisplayText(header[column])
                    : FormattableString.Invariant($"Column {column + 1}"),
                IsReadOnly = true,
                CellTemplate = CellTemplate(column),
            });
        }

        Grid.ItemsSource = data;
        Caption.Text = FormattableString.Invariant($"{title}: {DataRowCount} rows, {ColumnCount} columns");
        AutomationProperties.SetName(Grid, Caption.Text);
        Notice.Text = result.Notice ?? string.Empty;
        Notice.IsVisible = result.Notice is not null;
    }

    private static string DisplayText(CsvReader.CsvCell cell) => cell.FormulaLike ? FormulaMark + cell.Text : cell.Text;

    private static FuncDataTemplate<string[]> CellTemplate(int column) =>
        new((row, _) => new TextBlock
        {
            Text = row is not null && column < row.Length ? row[column] : string.Empty,
            TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
        }, supportsRecycling: false);

    private void Clear()
    {
        Grid.ItemsSource = null;
        Grid.Columns.Clear();
        Notice.IsVisible = false;
        DataRowCount = 0;
        ColumnCount = 0;
    }
}
