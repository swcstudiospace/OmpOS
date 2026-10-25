using System.Collections.Frozen;

namespace OmpOS.Native.Views.Editor;

/// <summary>
/// Maps a workspace file to the TextMate grammar scope that highlights it (ADE-09). The grammars come from the
/// <c>TextMateSharp.Grammars</c> package; ADR-0007 records which grammar supplies each scope.
/// </summary>
internal static class EditorLanguages
{
    private static readonly FrozenDictionary<string, string> ScopeByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "source.cs",
        [".ts"] = "source.ts",
        [".mts"] = "source.ts",
        [".cts"] = "source.ts",
        [".json"] = "source.json",
        [".yaml"] = "source.yaml",
        [".yml"] = "source.yaml",
        [".md"] = "text.html.markdown",
        [".markdown"] = "text.html.markdown",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the grammar scope for a workspace path.</summary>
    /// <param name="relativePath">The normalised workspace-relative path of the file.</param>
    /// <returns>The grammar scope, or null when no grammar highlights the file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="relativePath"/> is null.</exception>
    public static string? ScopeFor(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        var name = relativePath[(relativePath.LastIndexOf('/') + 1)..];
        var dot = name.LastIndexOf('.');
        return dot <= 0 ? null : ScopeByExtension.GetValueOrDefault(name[dot..]);
    }
}
