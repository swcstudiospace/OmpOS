using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.Landing;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Autonogrammer.Native.Views.Landing;

/// <summary>
/// The Code landing: the four environment cards of the <see cref="CodeLandingModel"/> that is its data context, drawn in their fixed order when no
/// session is selected. A card's own button carries the card's accessible name; its next actions are buttons under it. A press only asks the model,
/// and the window decides what happens. The view rebuilds the cards when the model changes, and keeps the keyboard focus on the button it was on.
/// </summary>
internal partial class CodeLandingView : UserControl
{
    private CodeLandingModel? _model;

    /// <summary>Creates a landing with no model. It draws nothing.</summary>
    public CodeLandingView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelPropertyChanged;
        }

        _model = DataContext as CodeLandingModel;
        if (_model is not null)
        {
            _model.PropertyChanged += OnModelPropertyChanged;
        }

        RenderCards();
    }

    /// <summary>Moves keyboard focus to the first enabled button of the landing.</summary>
    /// <returns><see langword="true"/> when a button could take the focus.</returns>
    internal bool FocusFirst()
    {
        var first = this.GetVisualDescendants().OfType<Button>().FirstOrDefault(static button => button.IsEnabled && button.IsEffectivelyVisible);
        first.FocusWhenReady();
        return first is not null;
    }

    private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(CodeLandingModel.Cards), StringComparison.Ordinal))
        {
            RenderCards();
        }
    }

    private void RenderCards()
    {
        var focused = FocusedButtonName();
        var visible = _model is not null;
        LocalCard.IsVisible = visible;
        LinkedCard.IsVisible = visible;
        CloudCard.IsVisible = visible;
        RemoteCard.IsVisible = visible;
        if (_model is not null)
        {
            foreach (var card in _model.Cards)
            {
                var (frame, body) = SlotOf(card.Kind);
                RenderCard(frame, body, card);
            }
        }

        RestoreFocus(focused);
    }

    private (Border Frame, StackPanel Body) SlotOf(CodeLandingCardKind kind) => kind switch
    {
        CodeLandingCardKind.LocalMachine => (LocalCard, LocalBody),
        CodeLandingCardKind.LinkedMachine => (LinkedCard, LinkedBody),
        CodeLandingCardKind.Cloud => (CloudCard, CloudBody),
        _ => (RemoteCard, RemoteBody),
    };

    private void RenderCard(Border frame, StackPanel body, CodeLandingCard card)
    {
        AutomationProperties.SetName(frame, card.AccessibleName);
        body.Children.Clear();
        if (card.Primary is { } primary)
        {
            body.Children.Add(CreatePrimary(card, primary));
        }
        else
        {
            body.Children.Add(CreateHeader(card));
        }

        foreach (var row in card.Rows)
        {
            body.Children.Add(CreateButton(row, "ghost"));
        }

        foreach (var action in card.Actions)
        {
            body.Children.Add(CreateButton(action, "sm"));
        }
    }

    private Button CreatePrimary(CodeLandingCard card, CodeLandingButton primary)
    {
        var button = new Button
        {
            Classes = { "ghost" },
            Content = CreateHeader(card),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            IsEnabled = primary.IsEnabled,
        };
        AutomationProperties.SetName(button, card.AccessibleName);
        if (primary.Action is { } action)
        {
            button.Click += (_, _) => _model?.Request(action, primary.OptionId);
        }

        return button;
    }

    private Button CreateButton(CodeLandingButton spec, string classes)
    {
        var button = new Button
        {
            Content = spec.Label,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = spec.IsEnabled,
        };
        foreach (var name in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            button.Classes.Add(name);
        }

        AutomationProperties.SetName(button, spec.AccessibleName);
        if (spec.Action is { } action)
        {
            button.Click += (_, _) => _model?.Request(action, spec.OptionId);
        }

        return button;
    }

    private static StackPanel CreateHeader(CodeLandingCard card)
    {
        var header = new StackPanel { Spacing = 2 };
        header.Children.Add(CreateText(card.Title, "strong"));
        header.Children.Add(CreateText(card.Message, "muted"));
        header.Children.Add(CreateText(card.OmpText, "muted"));
        return header;
    }

    private static TextBlock CreateText(string text, string classes)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        foreach (var name in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            block.Classes.Add(name);
        }

        return block;
    }

    private string? FocusedButtonName()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        return focused is Button button && button.GetVisualAncestors().Contains(this)
            ? AutomationProperties.GetName(button)
            : null;
    }

    private void RestoreFocus(string? name)
    {
        if (name is null)
        {
            return;
        }

        this.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => string.Equals(AutomationProperties.GetName(button), name, StringComparison.Ordinal) && button.IsEnabled)
            .FocusWhenReady();
    }
}
