using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit.Document;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;
using EditorText = AvaloniaEdit.Document.TextDocument;

namespace OmpOS.Native.Views.Editor;

/// <summary>
/// The code editor (ADE-09): a tab strip over the open files, a find bar that shows the match count and moves between
/// matches, and a text editor highlighted by TextMate grammars for C#, TypeScript, JSON, YAML and Markdown.
/// </summary>
/// <remarks>
/// The view binds to an <see cref="EditorViewModel"/> through its data context. The editor shows one buffer at a time, the
/// selected tab's text. Edits flow into the tab's <see cref="OmpOS.Presentation.Editor.TextDocument"/>, so the model always holds the current text.
/// Keys: Enter and F3 move to the next match, Shift+Enter and Shift+F3 to the previous one, and Escape returns focus to
/// the editor.
/// </remarks>
internal partial class EditorView : UserControl
{
    private readonly RegistryOptions _registry = new(ThemeName.DarkPlus);
    private EditorViewModel? _model;
    private EditorText? _document;
    private TextMate.Installation? _textMate;
    private bool _loading;
    private bool _attached;

    /// <summary>Gets a value indicating whether TextMate colours the shown buffer: true while the view is in a window and the selected file has a grammar.</summary>
    public bool IsHighlighting => _textMate is not null;

    /// <summary>Creates the editor with no data context.</summary>
    public EditorView()
    {
        InitializeComponent();

        FindInput.TextChanged += OnFindTextChanged;
        FindInput.KeyDown += OnFindKeyDown;
        MatchCaseCheck.IsCheckedChanged += OnMatchCaseChanged;
        PreviousMatchButton.Click += (_, _) => Navigate(forward: false);
        NextMatchButton.Click += (_, _) => Navigate(forward: true);
        AddHandler(KeyDownEvent, OnViewKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>Moves the caret of the selected file to the start of a line and scrolls it into view. A line past the end goes to the last line; a file that is not loaded yet does nothing.</summary>
    /// <param name="line">The 1-based line.</param>
    internal void GoToLine(int line)
    {
        var document = CodeEditor.Document;
        if (document is null || document.LineCount == 0)
        {
            return;
        }

        var clamped = Math.Clamp(line, 1, document.LineCount);
        CodeEditor.TextArea.Caret.Line = clamped;
        CodeEditor.TextArea.Caret.Column = 1;
        CodeEditor.ScrollToLine(clamped);
    }

    /// <summary>Binds the view to the editor state in the data context and draws its current state.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        DetachModel();
        _model = DataContext as EditorViewModel;
        if (_model is not null)
        {
            _model.PropertyChanged += OnModelPropertyChanged;
            _model.MatchRevealRequested += OnMatchRevealRequested;
        }

        TabStrip.ItemsSource = _model?.Items;
        FindInput.Text = _model?.FindQuery ?? string.Empty;
        MatchCaseCheck.IsChecked = _model?.CaseSensitive ?? false;
        LoadSelectedTab();
        RenderFindStatus();
    }

    /// <summary>Starts the TextMate highlighting when the editor joins the visual tree.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _attached = true;
        LoadSelectedTab();
    }

    /// <summary>Stops the TextMate highlighting when the editor leaves the visual tree.</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _attached = false;
        ReleaseTextMate();
    }

    private void DetachModel()
    {
        if (_model is null)
        {
            return;
        }

        _model.PropertyChanged -= OnModelPropertyChanged;
        _model.MatchRevealRequested -= OnMatchRevealRequested;
        _model = null;
    }

    // Shows the selected tab's text in a fresh editor buffer and applies the grammar for its file type. Edits made while
    // the buffer is loading are not copied back, because the model already holds this text.
    //
    // A file without a grammar (no tab, or an extension with no scope) is drawn plain by releasing TextMate, never by clearing its grammar:
    // clearing it makes TextMate's model emit to its listeners from this thread while the tokenizer thread of the previous grammar may be
    // emitting too, and the two take the colouring transformer's lock and the model's listener lock in opposite orders, which hangs the UI thread.
    private void LoadSelectedTab()
    {
        var tab = _model?.SelectedTab;
        var scope = tab is null ? null : EditorLanguages.ScopeFor(tab.RelativePath);
        if (scope is null)
        {
            ReleaseTextMate();
        }
        else if (_attached)
        {
            _textMate ??= CodeEditor.InstallTextMate(_registry);
        }

        _loading = true;
        try
        {
            if (_document is not null)
            {
                _document.Changed -= OnEditorTextChanged;
            }

            _document = new EditorText(tab?.Document.Text ?? string.Empty);
            _document.Changed += OnEditorTextChanged;
            CodeEditor.Document = _document;
        }
        finally
        {
            _loading = false;
        }

        if (scope is not null)
        {
            _textMate?.SetGrammar(scope);
        }
    }

    private void ReleaseTextMate()
    {
        _textMate?.Dispose();
        _textMate = null;
    }

    private void OnEditorTextChanged(object? sender, DocumentChangeEventArgs e)
    {
        if (_loading || _model?.SelectedTab is not { } tab)
        {
            return;
        }

        tab.Document.Replace(e.Offset, e.RemovalLength, e.InsertedText.Text);
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(EditorViewModel.SelectedTab):
                LoadSelectedTab();
                break;
            case nameof(EditorViewModel.MatchStatus):
            case nameof(EditorViewModel.HasMatches):
                RenderFindStatus();
                break;
        }
    }

    private void OnMatchRevealRequested(object? sender, EventArgs e)
    {
        if (_model?.SelectedTab is not { } tab || _model.SelectedMatch is not { } match)
        {
            return;
        }

        CodeEditor.Select(tab.Document.GetOffset(match.Line, match.Column), match.Length);
        CodeEditor.ScrollToLine(match.Line + 1);
    }

    private void RenderFindStatus()
    {
        MatchStatusText.Text = _model?.MatchStatus ?? string.Empty;
        var hasMatches = _model?.HasMatches == true;
        PreviousMatchButton.IsEnabled = hasMatches;
        NextMatchButton.IsEnabled = hasMatches;
    }

    private void Navigate(bool forward)
    {
        if (_model is null)
        {
            return;
        }

        if (forward)
        {
            _model.NextMatch();
        }
        else
        {
            _model.PreviousMatch();
        }
    }

    private void OnFindTextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
    {
        if (_model is not null)
        {
            _model.FindQuery = FindInput.Text ?? string.Empty;
        }
    }

    private void OnMatchCaseChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_model is not null)
        {
            _model.CaseSensitive = MatchCaseCheck.IsChecked == true;
        }
    }

    private void OnFindKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                Navigate(forward: !IsShift(e));
                e.Handled = true;
                break;
            case Key.Escape:
                _ = CodeEditor.TextArea.Focus();
                e.Handled = true;
                break;
        }
    }

    private void OnViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.F3)
        {
            return;
        }

        Navigate(forward: !IsShift(e));
        e.Handled = true;
    }

    private static bool IsShift(KeyEventArgs e) => (e.KeyModifiers & KeyModifiers.Shift) != KeyModifiers.None;
}
