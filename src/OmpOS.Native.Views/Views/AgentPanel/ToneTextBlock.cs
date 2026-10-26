using Avalonia;
using Avalonia.Controls;

namespace OmpOS.Native.Views.AgentPanel;

/// <summary>
/// A <see cref="TextBlock"/> whose colour role is a <see cref="StatusTone"/>. The role selects a style class, so the colour comes from a
/// token and a template can bind the role without holding a colour.
/// </summary>
internal sealed class ToneTextBlock : TextBlock
{
    /// <summary>The colour role of the text.</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty =
        AvaloniaProperty.Register<ToneTextBlock, StatusTone>(nameof(Tone), StatusTone.Secondary);

    private StatusTone _applied = StatusTone.Secondary;

    /// <summary>Creates text in the secondary role.</summary>
    public ToneTextBlock()
    {
        Classes.Add(StatusGlyphs.ClassOf(_applied));
    }

    /// <summary>Gets or sets the colour role of the text.</summary>
    public StatusTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(TextBlock);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ToneProperty)
        {
            Classes.Remove(StatusGlyphs.ClassOf(_applied));
            _applied = Tone;
            Classes.Add(StatusGlyphs.ClassOf(_applied));
        }
    }
}
