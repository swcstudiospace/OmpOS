using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>Which action classes each tier allows.</summary>
public static class ComputerGrantPolicy
{
    private static readonly ComputerActionClass[] View =
    [
        ComputerActionClass.Screenshot,
        ComputerActionClass.Zoom,
        ComputerActionClass.ListWindows,
    ];

    private static readonly ComputerActionClass[] Pointer =
    [
        ComputerActionClass.PointerMove,
        ComputerActionClass.Click,
        ComputerActionClass.Drag,
        ComputerActionClass.Scroll,
    ];

    private static readonly ComputerActionClass[] FullOnly =
    [
        ComputerActionClass.Type,
        ComputerActionClass.Chord,
        ComputerActionClass.Clipboard,
    ];

    /// <summary>Local grants last at most eight hours.</summary>
    public static readonly TimeSpan LocalMaximum = TimeSpan.FromHours(8);

    /// <summary>Dispatch-origin grants last at most thirty minutes.</summary>
    public static readonly TimeSpan DispatchMaximum = TimeSpan.FromMinutes(30);

    /// <summary><see langword="true"/> when <paramref name="tier"/> allows <paramref name="actionClass"/>.</summary>
    /// <param name="tier">The grant tier.</param>
    /// <param name="actionClass">The action class.</param>
    /// <returns><see langword="true"/> when the tier is high enough.</returns>
    public static bool Allows(ComputerGrantTier tier, ComputerActionClass actionClass)
    {
        if (Contains(View, actionClass))
        {
            return true;
        }

        if (Contains(Pointer, actionClass))
        {
            return tier >= ComputerGrantTier.ClickOnly;
        }

        return tier >= ComputerGrantTier.Full && Contains(FullOnly, actionClass);
    }

    /// <summary>Maps a tool name to an action class. Unknown names are <see langword="null"/>.</summary>
    /// <param name="toolName">The tool name.</param>
    /// <returns>The class, or <see langword="null"/>.</returns>
    public static ComputerActionClass? FromToolName(string toolName) => toolName switch
    {
        "computer_screenshot" => ComputerActionClass.Screenshot,
        "computer_zoom" => ComputerActionClass.Zoom,
        "computer_list_windows" => ComputerActionClass.ListWindows,
        "computer_move" => ComputerActionClass.PointerMove,
        "computer_click" => ComputerActionClass.Click,
        "computer_drag" => ComputerActionClass.Drag,
        "computer_scroll" => ComputerActionClass.Scroll,
        "computer_type" => ComputerActionClass.Type,
        "computer_key" or "computer_chord" => ComputerActionClass.Chord,
        "computer_clipboard_read" or "computer_clipboard_write" => ComputerActionClass.Clipboard,
        _ => null,
    };

    private static bool Contains(ComputerActionClass[] set, ComputerActionClass actionClass)
    {
        for (int i = 0; i < set.Length; i++)
        {
            if (set[i] == actionClass)
            {
                return true;
            }
        }

        return false;
    }
}
