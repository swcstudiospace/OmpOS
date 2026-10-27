using Avalonia;
using Avalonia.Controls;

namespace OmpOS.Native.Views.AgentPanel;

/// <summary>
/// The chip that shows a status as words and a glyph: a 20 px bordered pill with the glyph of an <see cref="IconKey"/> and the status
/// <see cref="Text"/> in the colour role of a <see cref="Tone"/>. It is not a tab stop; the owner names it where a name is needed.
/// </summary>
internal partial class StatusChip : UserControl
{
    /// <summary>The status in words.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<StatusChip, string?>(nameof(Text));

    /// <summary>The glyph key from the glyph table.</summary>
    public static readonly StyledProperty<string?> IconKeyProperty =
        AvaloniaProperty.Register<StatusChip, string?>(nameof(IconKey));

    /// <summary>The colour role of the status words.</summary>
    public static readonly StyledProperty<StatusTone> ToneProperty =
        AvaloniaProperty.Register<StatusChip, StatusTone>(nameof(Tone), StatusTone.Secondary);

    private StatusTone _drawnTone = StatusTone.Secondary;

    /// <summary>Creates an empty chip.</summary>
    public StatusChip()
    {
        InitializeComponent();
        Label.Classes.Add(StatusGlyphs.ClassOf(_drawnTone));
    }

    /// <summary>Gets or sets the status in words.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Gets or sets the glyph key.</summary>
    public string? IconKey
    {
        get => GetValue(IconKeyProperty);
        set => SetValue(IconKeyProperty, value);
    }

    /// <summary>Gets or sets the colour role of the words.</summary>
    public StatusTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            Label.Text = Text;
        }
        else if (change.Property == IconKeyProperty)
        {
            Icon.IconKey = IconKey;
        }
        else if (change.Property == ToneProperty)
        {
            Label.Classes.Remove(StatusGlyphs.ClassOf(_drawnTone));
            _drawnTone = Tone;
            Label.Classes.Add(StatusGlyphs.ClassOf(_drawnTone));
        }
    }
}
