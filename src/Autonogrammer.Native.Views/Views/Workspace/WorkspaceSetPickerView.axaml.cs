using System.Text;
using Autonogrammer.Presentation.Editor;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Autonogrammer.Native.Views.Workspace;

/// <summary>
/// The workspace set picker: the name of the set, its folders with Make primary and Remove on each row, the findings of the rules, and the
/// Add folder, Import and Export buttons. It draws the bound <see cref="WorkspaceSetPicker"/> and sends every action to it; the folder and file
/// dialogs come from <see cref="Dialogs"/>. Start raises <see cref="StartRequested"/> and Done raises <see cref="CancelRequested"/>: the host reads
/// <see cref="WorkspaceSetPicker.Snapshot"/> and closes the view. Delete on a focused folder row removes it.
/// </summary>
internal partial class WorkspaceSetPickerView : UserControl
{
    private IWorkspaceSetPickerDialogs? _dialogs;

    /// <summary>Creates the picker with no data context.</summary>
    public WorkspaceSetPickerView()
    {
        InitializeComponent();
    }

    /// <summary>Raised when the user presses Start while the set can start. The host reads the folders from the model.</summary>
    internal event EventHandler? StartRequested;

    /// <summary>Raised when the user presses Done. The host closes the picker without starting.</summary>
    internal event EventHandler? CancelRequested;

    /// <summary>
    /// Gets or sets the dialogs the Add folder, Import and Export buttons open. The default opens the platform dialogs over the top level the view is in.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    internal IWorkspaceSetPickerDialogs Dialogs
    {
        get => _dialogs ??= new StorageProviderWorkspaceSetPickerDialogs(() => TopLevel.GetTopLevel(this));
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            _dialogs = value;
        }
    }

    /// <summary>Gets or sets whether the footer with the Done and Start buttons is shown. A host that has its own Start action hides it.</summary>
    internal bool ShowFooter
    {
        get => FooterBar.IsVisible;
        set => FooterBar.IsVisible = value;
    }

    /// <summary>Gets the name box.</summary>
    internal TextBox NameInput => NameBox;

    /// <summary>Gets the sentence shown while the set has no folder.</summary>
    internal TextBlock EmptyMessage => EmptyText;

    /// <summary>Gets the list of folder rows.</summary>
    internal ItemsControl Folders => FolderRows;

    /// <summary>Gets the Add folder button.</summary>
    internal Button AddFolder => AddFolderButton;

    /// <summary>Gets the Import button.</summary>
    internal Button Import => ImportButton;

    /// <summary>Gets the Export button.</summary>
    internal Button Export => ExportButton;

    /// <summary>Gets the Start button.</summary>
    internal Button Start => StartButton;

    /// <summary>Gets the Done button.</summary>
    internal Button Done => DoneButton;

    /// <summary>Gets the assertive live region that says why an import or export did not happen.</summary>
    internal TextBlock LastErrorLine => LastErrorText;

    /// <summary>Gets the polite live region of the entries an import skipped.</summary>
    internal ItemsControl ImportWarnings => ImportWarningList;

    /// <summary>Gets the polite live region of the findings of the rules.</summary>
    internal ItemsControl Findings => FindingList;

    /// <summary>Builds the file name the Export dialog offers: the name of the set with characters a file name cannot hold replaced, and the extension.</summary>
    /// <param name="setName">The name of the set.</param>
    /// <returns>The file name.</returns>
    internal static string SuggestedFileName(string setName)
    {
        ArgumentNullException.ThrowIfNull(setName);

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(setName.Length);
        foreach (var character in setName.Trim())
        {
            builder.Append(Array.IndexOf(invalid, character) >= 0 ? '-' : character);
        }

        return string.Concat(builder.ToString(), ".", WorkspaceSetPickerCopy.CodeWorkspaceExtension);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnKeyDown(e);
        if (e.Handled || e.Key != Key.Delete || e.KeyModifiers != KeyModifiers.None || DataContext is not WorkspaceSetPicker picker)
        {
            return;
        }

        if ((e.Source as StyledElement)?.DataContext is WorkspacePickerRow row)
        {
            e.Handled = true;
            RemoveRow(picker, row);
        }
    }

    private void OnNameLostFocus(object? sender, RoutedEventArgs e)
    {
        // The model ignores a blank name and trims the rest, so the box shows the name the model kept.
        if (DataContext is WorkspaceSetPicker picker && !string.Equals(NameBox.Text, picker.Name, StringComparison.Ordinal))
        {
            NameBox.Text = picker.Name;
        }
    }

    private void OnMakePrimaryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceSetPicker picker && sender is Button { DataContext: WorkspacePickerRow row } && picker.MakePrimary(row.Id))
        {
            FocusRowLater(0);
        }
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceSetPicker picker && sender is Button { DataContext: WorkspacePickerRow row })
        {
            RemoveRow(picker, row);
        }
    }

    private void OnAddFolderClick(object? sender, RoutedEventArgs e) => _ = AddFolderAsync();

    private void OnImportClick(object? sender, RoutedEventArgs e) => _ = ImportAsync();

    private void OnExportClick(object? sender, RoutedEventArgs e) => _ = ExportAsync();

    private void OnStartClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceSetPicker { CanStart: true })
        {
            StartRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnDoneClick(object? sender, RoutedEventArgs e) => CancelRequested?.Invoke(this, EventArgs.Empty);

    private async Task AddFolderAsync()
    {
        if (DataContext is not WorkspaceSetPicker picker)
        {
            return;
        }

        var path = await AskAsync(Dialogs.PickFolderAsync).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(path))
        {
            picker.AddFolder(path);
        }
    }

    private async Task ImportAsync()
    {
        if (DataContext is not WorkspaceSetPicker picker)
        {
            return;
        }

        var path = await AskAsync(Dialogs.PickImportFileAsync).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(path))
        {
            _ = picker.ImportCodeWorkspace(path);
        }
    }

    private async Task ExportAsync()
    {
        if (DataContext is not WorkspaceSetPicker picker)
        {
            return;
        }

        var suggested = SuggestedFileName(picker.Name);
        var path = await AskAsync(token => Dialogs.PickExportFileAsync(suggested, token)).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(path))
        {
            _ = picker.ExportCodeWorkspace(path);
        }
    }

    // A dialog the platform cancels answers null; a cancelled token is the same answer.
    private static async Task<string?> AskAsync(Func<CancellationToken, Task<string?>> dialog)
    {
        try
        {
            return await dialog(CancellationToken.None).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private void RemoveRow(WorkspaceSetPicker picker, WorkspacePickerRow row)
    {
        var index = picker.Rows.IndexOf(row);
        if (picker.Remove(row.Id))
        {
            FocusRowLater(index);
        }
    }

    // The row a user acted on may be gone or moved, so the focus goes to the row now at that place, or to Add folder when none is left.
    private void FocusRowLater(int index) =>
        Dispatcher.UIThread.Post(() => FocusRowAt(index), DispatcherPriority.Loaded);

    private void FocusRowAt(int index)
    {
        var rows = FolderRows.GetVisualDescendants()
            .OfType<Border>()
            .Where(static border => border.Classes.Contains("folder-row") && border.IsEffectivelyVisible)
            .ToList();
        var target = rows.Count == 0 ? (Control)AddFolderButton : rows[Math.Clamp(index, 0, rows.Count - 1)];
        _ = target.Focus();
    }
}
