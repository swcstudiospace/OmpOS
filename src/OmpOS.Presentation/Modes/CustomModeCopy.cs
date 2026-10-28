using System.Globalization;

namespace OmpOS.Presentation.Modes;

/// <summary>The words of custom modes.</summary>
public static class CustomModeCopy
{
    /// <summary>The label of the palette action that pins a skill.</summary>
    public const string UseAsModeLabel = "Use as Mode";

    /// <summary>The label of the control that exits the mode.</summary>
    public const string ExitLabel = "Exit mode";

    /// <summary>The hint shown beside a skill in the slash palette.</summary>
    public const string AltEnterHint = "Alt+Enter: use as mode";

    /// <summary>The sentence shown while no skill can be pinned.</summary>
    public const string Empty = "No skills to use as a mode. Add a skill with a SKILL.md to .ompos/skills, .cursor/skills or another skills folder.";

    /// <summary>The status line when the mode was exited.</summary>
    public const string Exited = "Mode off. Prompts go as you write them.";

    /// <summary>The status line while the skills load.</summary>
    public const string Loading = "Loading skills.";

    /// <summary>The most characters of a skill body a mode may pin.</summary>
    public const int MaxBodyChars = 20000;

    /// <summary>Gets the text of the mode chip.</summary>
    /// <param name="name">The skill name.</param>
    /// <returns>The chip text.</returns>
    public static string ChipText(string name) => "Mode: " + name;

    /// <summary>Gets the accessible name of the mode chip.</summary>
    /// <param name="name">The skill name.</param>
    /// <returns>The name. It says that the mode is on and how to leave it.</returns>
    public static string ChipName(string name) => "Custom mode " + name + " is on. Activate to exit the mode.";

    /// <summary>Gets the status line for a pinned mode.</summary>
    /// <param name="name">The skill name.</param>
    /// <returns>The sentence.</returns>
    public static string Started(string name) => "Mode " + name + " is on. Its instructions go with every prompt until you exit the mode.";

    /// <summary>Gets the sentence for a skill that is not in the list.</summary>
    /// <param name="name">The name asked for.</param>
    /// <returns>The sentence.</returns>
    public static string Unknown(string name) => "There is no skill named " + name + " to use as a mode.";

    /// <summary>Gets the sentence for a skill whose body cannot be read.</summary>
    /// <param name="name">The skill name.</param>
    /// <returns>The sentence.</returns>
    public static string Unreadable(string name) => "The skill " + name + " could not be read, so it was not pinned.";

    /// <summary>Gets the sentence for a skill whose body is empty.</summary>
    /// <param name="name">The skill name.</param>
    /// <returns>The sentence.</returns>
    public static string NoBody(string name) => "The skill " + name + " has no instructions to pin.";

    /// <summary>Gets the sentence for a skill whose body is too long.</summary>
    /// <param name="name">The skill name.</param>
    /// <returns>The sentence.</returns>
    public static string TooLarge(string name) => string.Create(CultureInfo.InvariantCulture, $"The skill {name} is longer than {MaxBodyChars} characters, so it cannot be pinned on every prompt.");

    /// <summary>Gets the sentence for a failure.</summary>
    /// <param name="reason">The failure.</param>
    /// <returns>The sentence.</returns>
    public static string Failed(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        return "Custom mode failed: " + reason.TrimEnd('.') + ".";
    }
}
