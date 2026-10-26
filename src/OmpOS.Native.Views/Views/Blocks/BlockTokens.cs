using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace OmpOS.Native.Views.Blocks;

/// <summary>
/// Reads the shared design tokens for controls built in code (markdown and A2UI content), so they match the XAML
/// views. Sizes, colours, fonts and weights come from the application resources.
/// </summary>
internal static class BlockTokens
{
    /// <summary>The monospace face for code, read from the <c>OmpOS.Font.Mono</c> token.</summary>
    public static FontFamily Monospace => Get<FontFamily>("OmpOS.Font.Mono");

    /// <summary>The widest an inline image is drawn, so one large picture cannot fill the transcript.</summary>
    public const double MaxImageWidth = 480;

    /// <summary>
    /// Binds a property of a control to a design token, so the value follows the theme variant: the binding re-reads the token when the
    /// application or the window changes variant. Use it for every colour a control built in code draws; a one-time <see cref="Get{T}"/>
    /// would keep the colours of the variant that was showing when the control was built.
    /// </summary>
    /// <param name="target">The control that draws the value.</param>
    /// <param name="property">The property to bind, for example <see cref="Border.BackgroundProperty"/>.</param>
    /// <param name="key">The token key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="property"/> is null.</exception>
    public static void Bind(Control target, AvaloniaProperty property, string key)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(property);
        _ = target.Bind(property, target.GetResourceObservable(key));
    }

    /// <summary>Gets a design token from the application resources.</summary>
    /// <typeparam name="T">The token's value type.</typeparam>
    /// <param name="key">The token key.</param>
    /// <returns>The token value.</returns>
    /// <exception cref="InvalidOperationException">The application does not define the token.</exception>
    public static T Get<T>(string key)
    {
        // Colour roles live inside the Light and Dark theme variants. A lookup with no theme reads only the Default
        // layer and misses them, so resolve with the variant the application is showing.
        if (Application.Current is null || !Application.Current.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) || value is null)
        {
            throw new InvalidOperationException($"The design token '{key}' is not defined.");
        }

        return (T)value;
    }
}
