namespace OmpOS.Workspace.Conventions;

/// <summary>Reads one convention file for the scanner, turning every failure into a diagnostic so that nothing throws.</summary>
internal sealed class ConventionFileReader
{
    private readonly ConventionScanContext _context;

    /// <summary>Initializes a new instance of the <see cref="ConventionFileReader"/> class.</summary>
    /// <param name="context">The scan context.</param>
    public ConventionFileReader(ConventionScanContext context)
    {
        _context = context;
    }

    /// <summary>Reads a text file.</summary>
    /// <param name="path">The file path in the source.</param>
    /// <param name="relative">The path relative to the base directory, with <c>/</c> separators.</param>
    /// <param name="item">The kind of convention the file belongs to.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text, or <see langword="null"/> after a diagnostic was recorded.</returns>
    public async Task<string?> ReadTextAsync(string path, string relative, ConventionItemKind item, CancellationToken cancellationToken)
    {
        var result = await _context.Source.ReadTextAsync(path, _context.Options.MaxFileBytes, cancellationToken).ConfigureAwait(false);
        if (result.IsRead)
        {
            return result.Text;
        }

        Report(item, relative, result.ProblemKind, result.Problem ?? "the file cannot be read");
        return null;
    }

    /// <summary>Reads a markdown file and splits its frontmatter.</summary>
    /// <param name="path">The file path in the source.</param>
    /// <param name="relative">The path relative to the base directory, with <c>/</c> separators.</param>
    /// <param name="item">The kind of convention the file belongs to.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The split file, or <see langword="null"/> after a diagnostic was recorded.</returns>
    public async Task<ConventionFrontmatter?> ReadMarkdownAsync(string path, string relative, ConventionItemKind item, CancellationToken cancellationToken)
    {
        var text = await ReadTextAsync(path, relative, item, cancellationToken).ConfigureAwait(false);
        if (text is null)
        {
            return null;
        }

        var parsed = ConventionFrontmatterParser.Parse(text);
        if (parsed.Problem is null)
        {
            return parsed;
        }

        Report(item, relative, ConventionDiagnosticKind.Malformed, "malformed frontmatter: " + parsed.Problem);
        return null;
    }

    /// <summary>Records a problem for a file of this scan.</summary>
    /// <param name="item">The kind of convention the file belongs to.</param>
    /// <param name="relative">The path relative to the base directory, with <c>/</c> separators.</param>
    /// <param name="kind">What went wrong.</param>
    /// <param name="reason">One plain sentence that says why.</param>
    public void Report(ConventionItemKind item, string relative, ConventionDiagnosticKind kind, string reason) =>
        _context.Collector.AddDiagnostic(item, _context.OriginPrefix + relative, kind, reason);
}
