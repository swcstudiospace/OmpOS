using System.Globalization;

namespace Autonogrammer.Native.Settings;

/// <summary>The sentences the workspace sets source and its folder checks put in front of the user when a read, a write or a file export fails.</summary>
internal static class WorkspacesSourceCopy
{
    /// <summary>The reason an export of an empty set is refused.</summary>
    internal const string NothingToExport = "Add at least one folder before exporting the set.";

    /// <summary>The reason a set without a folder is not saved.</summary>
    internal const string NothingToSave = "Add at least one folder before saving the set.";

    /// <summary>The reason a blank set name is not saved.</summary>
    internal const string NameBlank = "Give the set a name before saving it.";

    /// <summary>The reason a set name the slash commands cannot use is not saved.</summary>
    internal const string NameUnusable = "The name of a set is at most 80 characters and has no control character.";

    /// <summary>Gets the reason the list of saved sets could not be read.</summary>
    /// <param name="detail">What the system reported.</param>
    /// <returns>The sentence.</returns>
    internal static string ReadFailed(string detail) =>
        string.Create(CultureInfo.InvariantCulture, $"The saved workspace sets could not be read: {detail}");

    /// <summary>Gets the reason a set could not be written.</summary>
    /// <param name="detail">What the system reported.</param>
    /// <returns>The sentence.</returns>
    internal static string SaveFailed(string detail) =>
        string.Create(CultureInfo.InvariantCulture, $"The workspace set could not be saved: {detail}");

    /// <summary>Gets the reason a set could not be forgotten.</summary>
    /// <param name="detail">What the system reported.</param>
    /// <returns>The sentence.</returns>
    internal static string ForgetFailed(string detail) =>
        string.Create(CultureInfo.InvariantCulture, $"The workspace set could not be forgotten: {detail}");
}
