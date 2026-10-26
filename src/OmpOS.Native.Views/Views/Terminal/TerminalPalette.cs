using Avalonia.Styling;
using XTerm.Options;

namespace OmpOS.Native.Views.Terminal;

/// <summary>
/// The sixteen ANSI colours of the omp terminal in each theme variant (docs/plans/ui-parity/cursor-agents-window-parity.md, 2.7).
/// Each normal and bright colour reads at 4.5:1 or more on the terminal background, which is <c>SurfaceSunken</c>: <c>#141414</c> in
/// Dark and <c>#f0f0ee</c> in Light. These values are terminal data, not UI colour roles, so they live here and not in the token
/// dictionary; the terminal control keeps its background, foreground, cursor and selection on the tokens.
/// </summary>
internal static class TerminalPalette
{
    /// <summary>Gets the ANSI theme for a variant. The Light variant gets the light palette; every other variant, including the default, gets the dark one.</summary>
    /// <param name="variant">The theme variant the control is showing.</param>
    /// <returns>A new theme holding the sixteen ANSI colours. The caller sets nothing else on it.</returns>
    public static ThemeOptions For(ThemeVariant variant) => variant == ThemeVariant.Light ? Light() : Dark();

    private static ThemeOptions Dark() => new()
    {
        Black = "#808080",
        Red = "#ff7b8e",
        Green = "#5fd38a",
        Yellow = "#f1b467",
        Blue = "#7aa2f7",
        Magenta = "#c4a7ff",
        Cyan = "#6fd0e0",
        White = "#d4d4d4",
        BrightBlack = "#8a8a8a",
        BrightRed = "#ff9aa9",
        BrightGreen = "#86e3a8",
        BrightYellow = "#f6cb8b",
        BrightBlue = "#9db9fa",
        BrightMagenta = "#d6c2ff",
        BrightCyan = "#94dfeb",
        BrightWhite = "#f4f4f4",
    };

    private static ThemeOptions Light() => new()
    {
        Black = "#1c1c1a",
        Red = "#b3261e",
        Green = "#166534",
        Yellow = "#7a4a07",
        Blue = "#1d4ed8",
        Magenta = "#6d28d9",
        Cyan = "#0e6577",
        White = "#55555a",
        BrightBlack = "#55555a",
        BrightRed = "#c0392b",
        BrightGreen = "#1b7a3f",
        BrightYellow = "#8a5a00",
        BrightBlue = "#2557d6",
        BrightMagenta = "#7c3aed",
        BrightCyan = "#0f7488",
        BrightWhite = "#3f3f46",
    };
}
