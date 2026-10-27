using System.Globalization;

namespace OmpOS.Presentation.Settings;

/// <summary>
/// Every text of the Keyboard page, in one place so the view carries no literal. The page lists the shortcuts the shell handles and
/// says that they cannot be remapped.
/// </summary>
public static class KeyboardCopy
{
    /// <summary>The navigation label of the Keyboard page.</summary>
    public const string Title = "Keyboard";

    /// <summary>The heading of the Keyboard page.</summary>
    public const string Heading = "Keyboard shortcuts";

    /// <summary>The sentence under the heading. It says that remapping is left out on purpose.</summary>
    public const string Description =
        "The keyboard shortcuts OmpOS handles. They are fixed: remapping is left out on purpose, so this page only lists them.";

    /// <summary>The text shown when the source lists no shortcut.</summary>
    public const string Empty = "No shortcuts were reported.";

    /// <summary>The scope of the shortcuts that work anywhere in the main window.</summary>
    public const string ScopeApplication = "Application";

    /// <summary>The scope of the shortcuts of the agent list and the agent thread.</summary>
    public const string ScopeAgents = "Agents";

    /// <summary>The scope of the shortcuts of the prompt box.</summary>
    public const string ScopeComposer = "Composer";

    /// <summary>The scope of the shortcuts of the Kanban board.</summary>
    public const string ScopeKanban = "Kanban";

    /// <summary>The scope of the shortcuts of the Settings window.</summary>
    public const string ScopeSettings = "Settings";

    /// <summary>Gets the scopes in the order the page draws them. A scope that is not listed here follows them in the order the source reported it.</summary>
    public static IReadOnlyList<string> ScopeOrder { get; } = [ScopeApplication, ScopeAgents, ScopeComposer, ScopeKanban, ScopeSettings];

    /// <summary>Builds the sentence a screen reader speaks for a shortcut row.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The action, a colon and the keys.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> is null.</exception>
    public static string RowSummary(ShortcutRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return string.Create(CultureInfo.InvariantCulture, $"{row.Action}: {row.Gesture}");
    }
}
