namespace OmpOS.Presentation.Settings.Customize;

/// <summary>What a Customize source read.</summary>
/// <param name="Entries">The entries to list, in the order to show them.</param>
/// <param name="Problems">One sentence per file that could not be read (for example a file that is too large), or an empty list.</param>
public sealed record CustomizeCatalog(IReadOnlyList<CustomizeEntry> Entries, IReadOnlyList<string> Problems);
