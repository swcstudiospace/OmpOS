using System.Globalization;

namespace Autonogrammer.Native.Views.Blocks;

/// <summary>The words the block views write themselves, in one place so a view's markup holds none of them.</summary>
internal static class BlockText
{
    /// <summary>The label of the row that collapses an expanded file edit again.</summary>
    internal const string ShowFewerLines = "Show fewer lines";

    /// <summary>The sign column of an added line.</summary>
    internal const string AddedSign = "+";

    /// <summary>The sign column of a removed line.</summary>
    internal const string RemovedSign = "-";

    /// <summary>The label of the row that shows the lines a collapsed file edit hides.</summary>
    /// <param name="hidden">How many lines are hidden.</param>
    /// <returns>For example <c>Show 12 more lines</c>.</returns>
    internal static string ShowMoreLines(int hidden) =>
        string.Create(CultureInfo.InvariantCulture, $"Show {hidden} more {(hidden == 1 ? "line" : "lines")}");

    /// <summary>The count of added lines in the header of a file edit.</summary>
    /// <param name="added">The number of added lines.</param>
    /// <returns>For example <c>+3</c>.</returns>
    internal static string AddedCount(int added) => string.Create(CultureInfo.InvariantCulture, $"+{added}");

    /// <summary>The count of removed lines in the header of a file edit.</summary>
    /// <param name="removed">The number of removed lines.</param>
    /// <returns>For example <c>-2</c>.</returns>
    internal static string RemovedCount(int removed) => string.Create(CultureInfo.InvariantCulture, $"-{removed}");
}
