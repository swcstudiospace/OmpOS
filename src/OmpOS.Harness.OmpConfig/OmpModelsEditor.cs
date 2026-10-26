namespace OmpOS.Harness.OmpConfig;

/// <summary>
/// The pure text half of the <c>models.yml</c> editor: given the file text it computes the new text for an upsert or a removal, or
/// the reason it refuses. Everything outside an OmpOS block is kept byte for byte, because the file is spliced line by line and
/// never re-serialised. No file or process is touched here.
/// </summary>
internal static class OmpModelsEditor
{
    /// <summary>Adds or replaces the block of <paramref name="provider"/>.</summary>
    /// <param name="text">The current file text; empty for a missing file.</param>
    /// <param name="provider">A provider that passed validation.</param>
    /// <returns>The decision.</returns>
    public static OmpModelsEdit Upsert(string text, OmpManagedProvider provider)
    {
        var layout = OmpModelsLayout.Analyze(text);
        if (layout.Unsupported is not null)
        {
            return new OmpModelsEdit(OmpModelsWriteStatus.UnsupportedLayout, null, layout.Unsupported);
        }

        if (layout.ForeignKeys.Contains(provider.Id))
        {
            return new OmpModelsEdit(
                OmpModelsWriteStatus.Conflict,
                null,
                $"models.yml already defines a provider named {provider.Id} that OmpOS did not create. Choose another id or remove that entry by hand.");
        }

        var lines = new List<OmpYamlLine>(layout.Lines);
        var existing = layout.Blocks.FirstOrDefault(block => string.Equals(block.Id, provider.Id, StringComparison.Ordinal));
        var rendered = OmpManagedBlockWriter.Render(provider, layout.ChildIndent, layout.Step);

        if (existing.Id is not null)
        {
            var replacement = ToLines(rendered, layout.EolStyle, lines[existing.End].Eol);
            var current = lines.GetRange(existing.Begin, existing.End - existing.Begin + 1);
            if (current.SequenceEqual(replacement))
            {
                return new OmpModelsEdit(OmpModelsWriteStatus.Unchanged, null, $"The provider {provider.Id} is already up to date.");
            }

            lines.RemoveRange(existing.Begin, existing.End - existing.Begin + 1);
            lines.InsertRange(existing.Begin, replacement);
            return Written(layout, lines, $"The provider {provider.Id} was updated.");
        }

        if (layout.ProvidersLine >= 0)
        {
            InsertBlock(lines, layout.InsertAfter, rendered, layout.EolStyle);
            return Written(layout, lines, $"The provider {provider.Id} was added.");
        }

        var providersLine = "providers: " + OmpModelsLayout.CreatedMarker;
        var created = new List<string> { providersLine };
        created.AddRange(rendered);
        InsertBlock(lines, lines.Count - 1, created, layout.EolStyle);
        return Written(layout, lines, $"The provider {provider.Id} was added.");
    }

    /// <summary>Removes the block of <paramref name="id"/>, and the <c>providers:</c> line too when OmpOS created it and nothing else is left under it.</summary>
    /// <param name="text">The current file text.</param>
    /// <param name="id">The provider id.</param>
    /// <returns>The decision. A missing block is <see cref="OmpModelsWriteStatus.Unchanged"/>; a foreign entry with that id is <see cref="OmpModelsWriteStatus.Conflict"/>.</returns>
    public static OmpModelsEdit Remove(string text, string id)
    {
        var layout = OmpModelsLayout.Analyze(text);
        if (layout.Unsupported is not null)
        {
            return new OmpModelsEdit(OmpModelsWriteStatus.UnsupportedLayout, null, layout.Unsupported);
        }

        var block = layout.Blocks.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));
        if (block.Id is null)
        {
            return layout.ForeignKeys.Contains(id)
                ? new OmpModelsEdit(
                    OmpModelsWriteStatus.Conflict,
                    null,
                    $"models.yml defines {id}, but OmpOS did not create it, so OmpOS does not remove it. Remove it by hand.")
                : new OmpModelsEdit(OmpModelsWriteStatus.Unchanged, null, $"There is no OmpOS provider named {id}.");
        }

        var lines = new List<OmpYamlLine>(layout.Lines);
        var originalLastLineHadNoEol = lines[^1].Eol.Length == 0;
        var removedLastLine = block.End == lines.Count - 1;
        var removedCount = block.End - block.Begin + 1;
        lines.RemoveRange(block.Begin, removedCount);

        var sectionEnd = layout.SectionEnd - removedCount;
        if (layout.ProvidersCreatedByOmpOs && SectionIsBlank(lines, layout.ProvidersLine + 1, sectionEnd))
        {
            lines.RemoveAt(layout.ProvidersLine);
        }

        if (originalLastLineHadNoEol && removedLastLine && lines.Count > 0)
        {
            lines[^1] = lines[^1] with { Eol = string.Empty };
        }

        return new OmpModelsEdit(OmpModelsWriteStatus.Written, layout.Join(lines), $"The provider {id} was removed.");
    }

    /// <summary>Reads the managed providers back from the OmpOS blocks of <paramref name="text"/>.</summary>
    /// <param name="text">The file text.</param>
    /// <returns>The providers and one problem sentence per block or layout that could not be read.</returns>
    public static OmpManagedReadResult ReadManaged(string text)
    {
        var layout = OmpModelsLayout.Analyze(text);
        if (layout.Unsupported is not null)
        {
            return new OmpManagedReadResult([], [layout.Unsupported]);
        }

        var providers = new List<OmpManagedProvider>();
        var problems = new List<string>();
        foreach (var block in layout.Blocks)
        {
            var inner = new List<string>(block.End - block.Begin - 1);
            for (var index = block.Begin + 1; index < block.End; index++)
            {
                inner.Add(layout.Lines[index].Content);
            }

            if (OmpManagedBlockReader.TryParse(block.Id, inner, out var provider, out var problem))
            {
                providers.Add(provider!);
            }
            else
            {
                problems.Add(problem);
            }
        }

        return new OmpManagedReadResult(providers, problems);
    }

    private static OmpModelsEdit Written(OmpModelsLayout layout, List<OmpYamlLine> lines, string message) =>
        new(OmpModelsWriteStatus.Written, layout.Join(lines), message);

    private static bool SectionIsBlank(List<OmpYamlLine> lines, int start, int end)
    {
        for (var index = start; index < end; index++)
        {
            if (!OmpModelsLayout.IsBlank(lines[index].Content))
            {
                return false;
            }
        }

        return true;
    }

    private static List<OmpYamlLine> ToLines(List<string> contents, string eol, string lastEol)
    {
        var lines = new List<OmpYamlLine>(contents.Count);
        for (var index = 0; index < contents.Count; index++)
        {
            lines.Add(new OmpYamlLine(contents[index], index == contents.Count - 1 ? lastEol : eol));
        }

        return lines;
    }

    // Inserts after lines[after] (-1 inserts at the start). When that line is the last one and has no line ending, it gains one and
    // the new last line takes its place without one, so a file that had no final newline still has none.
    private static void InsertBlock(List<OmpYamlLine> lines, int after, List<string> contents, string eol)
    {
        var atEnd = after == lines.Count - 1;
        var lastEol = eol;
        if (atEnd && lines.Count > 0 && lines[after].Eol.Length == 0)
        {
            lines[after] = lines[after] with { Eol = eol };
            lastEol = string.Empty;
        }

        lines.InsertRange(after + 1, ToLines(contents, eol, lastEol));
    }
}
