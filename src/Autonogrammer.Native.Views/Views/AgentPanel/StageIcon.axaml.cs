using Avalonia;
using Avalonia.Controls;
using Autonogrammer.Presentation.AgentPanel;

namespace Autonogrammer.Native.Views.AgentPanel;

/// <summary>
/// One status glyph of the glyph table, drawn from an <see cref="StatusIcon"/> key. It is decorative (raw accessibility view): the text next
/// to it carries the meaning. The Running glyph pulses only while the control sits under no <c>reduced-motion</c> class.
/// </summary>
internal partial class StageIcon : UserControl
{
    /// <summary>The style class that makes the glyph pulse.</summary>
    internal const string PulseClass = "running-pulse";

    /// <summary>The key of the glyph to draw. An unknown key draws the info glyph.</summary>
    public static readonly StyledProperty<string?> IconKeyProperty =
        AvaloniaProperty.Register<StageIcon, string?>(nameof(IconKey));

    private StatusTone _drawnTone = StatusTone.Secondary;

    /// <summary>Creates a glyph that draws nothing until it has a key.</summary>
    public StageIcon()
    {
        InitializeComponent();
        Redraw();
    }

    /// <summary>Gets or sets the key of the glyph to draw.</summary>
    public string? IconKey
    {
        get => GetValue(IconKeyProperty);
        set => SetValue(IconKeyProperty, value);
    }

    /// <summary>Gets whether the glyph carries the pulse class at the moment.</summary>
    internal bool IsPulsing => Glyph.Classes.Contains(PulseClass);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IconKeyProperty)
        {
            Redraw();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Redraw();
    }

    private void Redraw()
    {
        var key = IconKey;
        if (key is null)
        {
            Glyph.Data = null;
            Glyph.Classes.Remove(PulseClass);
            return;
        }

        Glyph.Data = StatusGlyphs.GeometryOf(key);

        Glyph.Classes.Remove(StatusGlyphs.ClassOf(_drawnTone));
        _drawnTone = StatusGlyphs.StrokeOf(key);
        Glyph.Classes.Add(StatusGlyphs.ClassOf(_drawnTone));

        var pulse = string.Equals(key, StatusIcon.Running, StringComparison.Ordinal) && !MotionProbe.IsReduced(this);
        if (pulse)
        {
            Glyph.Classes.Add(PulseClass);
        }
        else
        {
            Glyph.Classes.Remove(PulseClass);
        }
    }
}
