using Avalonia;
using Avalonia.Media;

namespace OmpOS.Native.Views.Blocks;

/// <summary>
/// Reads the shared design tokens for controls built in code (markdown and A2UI content), so they match the XAML
/// views. Sizes, colours and weights come from the application resources; only the monospace stack is defined here,
/// because the token set has no monospace entry.
/// </summary>
internal static class BlockTokens
{
    /// <summary>The monospace stack for code, with the generic family as the last fallback.</summary>
    public static readonly FontFamily Monospace = FontFamily.Parse("Menlo, Consolas, Liberation Mono, monospace");

    /// <summary>The widest an inline image is drawn, so one large picture cannot fill the transcript.</summary>
    public const double MaxImageWidth = 480;

    /// <summary>Gets a design token from the application resources.</summary>
    /// <typeparam name="T">The token's value type.</typeparam>
    /// <param name="key">The token key.</param>
    /// <returns>The token value.</returns>
    /// <exception cref="InvalidOperationException">The application does not define the token.</exception>
    public static T Get<T>(string key)
    {
        if (Application.Current is null || !Application.Current.TryGetResource(key, null, out var value) || value is null)
        {
            throw new InvalidOperationException($"The design token '{key}' is not defined.");
        }

        return (T)value;
    }
}
