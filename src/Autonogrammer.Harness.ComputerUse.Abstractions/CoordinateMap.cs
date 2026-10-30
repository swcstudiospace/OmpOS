namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>
/// The only place display scale is applied. X11 and Windows pass physical pixels (<see cref="DisplayInfo.ScaleFactor"/> 1).
/// macOS passes points through <see cref="DisplayInfo.ScaleFactor"/> (capture pixels divided by points).
/// Rounding is <see cref="MidpointRounding.ToEven"/>. This type does not read the clock and performs no I/O.
/// </summary>
public sealed class CoordinateMap
{
    private readonly IReadOnlyList<DisplayInfo> _displays;

    /// <summary>Creates a map from explicit display records. Callers do not pass a backend.</summary>
    /// <param name="displays">Displays with native bounds and scale factors.</param>
    public CoordinateMap(IReadOnlyList<DisplayInfo> displays)
    {
        ArgumentNullException.ThrowIfNull(displays);
        _displays = displays;
    }

    /// <summary>Maps a model point on <paramref name="displayId"/> to native virtual-desktop pixels.</summary>
    /// <param name="displayId">Display id.</param>
    /// <param name="model">Scaled point relative to that display's origin.</param>
    /// <returns>The native point.</returns>
    public NativePoint ScaleToNative(string displayId, ModelPoint model) =>
        ScaleToNative(Require(displayId), model);

    /// <summary>Maps a native point back to scaled units on <paramref name="displayId"/>.</summary>
    /// <param name="displayId">Display id.</param>
    /// <param name="native">Native virtual-desktop point.</param>
    /// <returns>The model point.</returns>
    public ModelPoint ScaleToModel(string displayId, NativePoint native) =>
        ScaleToModel(Require(displayId), native);

    /// <summary>Maps a model point to native pixels. Offset is the display origin, which may be negative.</summary>
    /// <param name="display">The display.</param>
    /// <param name="model">Scaled point relative to the display origin.</param>
    /// <returns>The native point, rounded with <see cref="MidpointRounding.ToEven"/>.</returns>
    public static NativePoint ScaleToNative(DisplayInfo display, ModelPoint model)
    {
        ArgumentNullException.ThrowIfNull(display);
        double scale = RequireScale(display);
        return new NativePoint
        {
            X = display.Bounds.X + RoundToEven(model.X * scale),
            Y = display.Bounds.Y + RoundToEven(model.Y * scale),
        };
    }

    /// <summary>Maps a native point back to scaled units on <paramref name="display"/>.</summary>
    /// <param name="display">The display.</param>
    /// <param name="native">Native virtual-desktop point.</param>
    /// <returns>The model point.</returns>
    public static ModelPoint ScaleToModel(DisplayInfo display, NativePoint native)
    {
        ArgumentNullException.ThrowIfNull(display);
        double scale = RequireScale(display);
        return new ModelPoint
        {
            X = (native.X - display.Bounds.X) / scale,
            Y = (native.Y - display.Bounds.Y) / scale,
        };
    }

    /// <summary><see langword="true"/> when <paramref name="point"/> lies inside <paramref name="display"/> native bounds.</summary>
    /// <param name="display">The display.</param>
    /// <param name="point">Native point.</param>
    /// <returns><see langword="true"/> when the point is inside, including a negative origin.</returns>
    public static bool IsInside(DisplayInfo display, NativePoint point)
    {
        ArgumentNullException.ThrowIfNull(display);
        PixelRect bounds = display.Bounds;
        return point.X >= bounds.X
            && point.Y >= bounds.Y
            && point.X < bounds.X + bounds.Width
            && point.Y < bounds.Y + bounds.Height;
    }

    /// <summary><see langword="true"/> when <paramref name="point"/> lies inside the scaled display.</summary>
    /// <param name="display">The display.</param>
    /// <param name="point">Model point relative to the display origin.</param>
    /// <returns><see langword="true"/> when the point is inside the scaled size.</returns>
    public static bool IsInside(DisplayInfo display, ModelPoint point)
    {
        ArgumentNullException.ThrowIfNull(display);
        double scale = RequireScale(display);
        double width = display.Bounds.Width / scale;
        double height = display.Bounds.Height / scale;
        return point.X >= 0 && point.Y >= 0 && point.X < width && point.Y < height;
    }

    private DisplayInfo Require(string displayId)
    {
        for (int i = 0; i < _displays.Count; i++)
        {
            if (string.Equals(_displays[i].Id, displayId, StringComparison.Ordinal))
            {
                return _displays[i];
            }
        }

        throw new ArgumentOutOfRangeException(nameof(displayId));
    }

    private static double RequireScale(DisplayInfo display)
    {
        if (display.ScaleFactor <= 0 || double.IsNaN(display.ScaleFactor) || double.IsInfinity(display.ScaleFactor))
        {
            throw new ArgumentOutOfRangeException(nameof(display));
        }

        return display.ScaleFactor;
    }

    private static int RoundToEven(double value) =>
        (int)Math.Round(value, MidpointRounding.ToEven);
}
