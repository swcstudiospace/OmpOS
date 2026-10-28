using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Autonogrammer.Contracts.Ui;
using Autonogrammer.Native.Views.Display;
using Autonogrammer.Presentation.Commands.Handlers;

namespace Autonogrammer.Native.Views.Blocks;

/// <summary>A code block: its language label, a copy control, and the code as monospace text.</summary>
internal partial class CodeBlockView : UserControl
{
    private readonly string _code;
    private readonly DisplayOptionsWatcher _display;

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
        LineNumbers.Text = NumberLines(block.Code);
        LineNumbers.FontFamily = BlockTokens.Monospace;
        _display = new DisplayOptionsWatcher(this, ApplyDisplay);
        ApplyDisplay(_display.Values);
    }

    /// <summary>Builds the gutter text: one number per line of the code, a trailing line break not counting as a line, at least one number.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The numbers, one per line.</returns>
    internal static string NumberLines(string code)
    {
        var lines = code.Split('\n').Length;
        if (lines > 1 && code.EndsWith('\n'))
        {
            lines--;
        }

        return string.Join('\n', Enumerable.Range(1, lines).Select(static number => number.ToString(CultureInfo.InvariantCulture)));
    }

    private void ApplyDisplay(DisplaySettingsValues values) => LineNumbers.IsVisible = values.ShowLineNumbers;

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
