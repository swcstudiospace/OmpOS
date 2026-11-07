using System.Collections.Frozen;
using Avalonia.Controls;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace Autonogrammer.Native.Views.Cowork.Artifacts;

/// <summary>
/// A code artifact as a read-only AvaloniaEdit buffer. TextMate colours it only when the language tag is one of the exact tags
/// below; any other tag, and no tag, is plain text. The view subscribes to no navigation event.
/// </summary>
public partial class ArtifactCodeView : UserControl
{
    private static readonly FrozenDictionary<string, string> ScopeByTag = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["csharp"] = "source.cs",
        ["cs"] = "source.cs",
        ["typescript"] = "source.ts",
        ["ts"] = "source.ts",
        ["json"] = "source.json",
        ["yaml"] = "source.yaml",
        ["yml"] = "source.yaml",
        ["markdown"] = "text.html.markdown",
        ["md"] = "text.html.markdown",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private readonly RegistryOptions _registry = new(ThemeName.DarkPlus);
    private TextMate.Installation? _textMate;
    private bool _attached;

    /// <summary>Creates the view.</summary>
    public ArtifactCodeView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) =>
        {
            _attached = true;
            ApplyHighlighting();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _attached = false;
            ReleaseHighlighting();
        };
    }

    /// <summary>Gets the TextMate scope applied to the shown text, or null when the text is plain.</summary>
    public string? HighlightScope { get; private set; }

    /// <summary>Gets the text shown.</summary>
    public string Text => Editor.Text ?? string.Empty;

    /// <summary>Shows code, replacing what was shown.</summary>
    /// <param name="language">The exact language tag, or null.</param>
    /// <param name="text">The text to show.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public void Show(string? language, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Editor.Text = text;
        HighlightScope = language is null ? null : ScopeByTag.GetValueOrDefault(language);
        ReleaseHighlighting();
        ApplyHighlighting();
    }

    private void ApplyHighlighting()
    {
        if (!_attached || HighlightScope is null)
        {
            return;
        }

        _textMate ??= Editor.InstallTextMate(_registry);
        _textMate.SetGrammar(HighlightScope);
    }

    private void ReleaseHighlighting()
    {
        _textMate?.Dispose();
        _textMate = null;
    }
}
