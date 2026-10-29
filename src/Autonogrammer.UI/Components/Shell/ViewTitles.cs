namespace Autonogrammer.UI.Components.Shell;

/// <summary>Builds the document titles of the web head. A title lists the most specific part first and ends with the product, for example <c>Kanban - Autonogrammer</c>.</summary>
public static class ViewTitles
{
    /// <summary>The product name that ends every title.</summary>
    public const string ProductName = "Autonogrammer";

    /// <summary>The text between two parts of a title.</summary>
    public const string Separator = " - ";

    /// <summary>Joins title parts, most specific first, and appends the product name.</summary>
    /// <param name="parts">The parts of the title, for example the session title then <c>Agents</c>.</param>
    /// <returns>The title; just the product name when there are no parts.</returns>
    public static string Compose(params string[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return parts.Length == 0 ? ProductName : string.Join(Separator, parts) + Separator + ProductName;
    }
}
