using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using OmpOS.Contracts.Ui;

namespace OmpOS.Native.Views.Blocks;

/// <summary>A code block: its language label, a copy control, and the code as monospace text.</summary>
internal partial class CodeBlockView : UserControl
{
    private readonly string _code;

    /// <summary>Builds the view for one code block.</summary>
    /// <param name="block">The code block.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    public CodeBlockView(CodeBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        InitializeComponent();
        _code = block.Code;
        LanguageLabel.Text = block.Language;
        LanguageLabel.IsVisible = block.Language.Length > 0;
        CodeText.Text = block.Code;
        CodeText.FontFamily = BlockTokens.Monospace;
        CopyButton.Click += OnCopyClick;
    }

    private void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            _ = CopyAsync(clipboard);
        }
    }

    private async Task CopyAsync(IClipboard clipboard) => await clipboard.SetTextAsync(_code).ConfigureAwait(true);
}
