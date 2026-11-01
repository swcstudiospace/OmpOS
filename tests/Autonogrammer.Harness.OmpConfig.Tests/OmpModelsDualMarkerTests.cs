using System.Runtime.Versioning;
using System.Text;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.OmpConfig.Tests;

/// <summary>The legacy marker family is read, rewritten under the new markers and removed, with unmanaged bytes untouched.</summary>
[UnsupportedOSPlatform("windows")]
public sealed class OmpModelsDualMarkerTests
{
    private const string NewBegin = "# autonogrammer:begin ";
    private const string NewEnd = "# autonogrammer:end ";

    private static readonly string[] BodyLines =
    [
        "    baseUrl: \"http://127.0.0.1:11434/v1\"",
        "    api: openai-completions",
        "    auth: none",
        "    models:",
        "      - id: \"small\"",
        "        reasoning: false",
        "        input: [text]",
    ];

    // ---- layout ----

    /// <summary>A file with only legacy markers yields one canonical block per id, recognized like a new block.</summary>
    [Fact]
    public void LegacyOnlyFileParsesAsOneCanonicalBlock()
    {
        var layout = OmpModelsLayout.Analyze("providers:\n" + Legacy("acme", "\n"));

        layout.Unsupported.ShouldBeNull();
        layout.Blocks.ShouldBe([new OmpManagedBlockSpan("acme", 1, 10)]);
        layout.LegacyBlocks.ShouldBeEmpty();
        layout.ForeignKeys.ShouldBeEmpty();
    }

    /// <summary>A file with only new markers parses as before.</summary>
    [Fact]
    public void NewOnlyFileParsesAsBefore()
    {
        var layout = OmpModelsLayout.Analyze("providers:\n" + Fresh("acme", "\n"));

        layout.Unsupported.ShouldBeNull();
        layout.Blocks.ShouldBe([new OmpManagedBlockSpan("acme", 1, 10)]);
        layout.LegacyBlocks.ShouldBeEmpty();
    }

    /// <summary>Different ids in different families give two canonical blocks in file order.</summary>
    [Fact]
    public void MixedIdsGiveTwoCanonicalBlocksInFileOrder()
    {
        var layout = OmpModelsLayout.Analyze("providers:\n" + Legacy("acme", "\n") + Fresh("beta", "\n"));

        layout.Unsupported.ShouldBeNull();
        layout.Blocks.Select(block => block.Id).ShouldBe(["acme", "beta"]);
        layout.LegacyBlocks.ShouldBeEmpty();
    }

    /// <summary>A same-id twin parses: the new span is canonical and the legacy span is recorded as superseded.</summary>
    [Fact]
    public void SameIdTwinKeepsTheNewSpanCanonicalAndRecordsTheLegacyOne()
    {
        var layout = OmpModelsLayout.Analyze("providers:\n" + Legacy("acme", "\n") + Fresh("acme", "\n"));

        layout.Unsupported.ShouldBeNull();
        layout.Blocks.ShouldBe([new OmpManagedBlockSpan("acme", 11, 20)]);
        layout.LegacyBlocks.ShouldBe([new OmpManagedBlockSpan("acme", 1, 10)]);
        layout.ForeignKeys.ShouldBeEmpty();
    }

    /// <summary>Two spans of one family for one id stay unsupported, in either family.</summary>
    [Fact]
    public void TwoSpansOfTheSameFamilyStayUnsupported()
    {
        OmpModelsLayout.Analyze("providers:\n" + Legacy("acme", "\n") + Legacy("acme", "\n")).Unsupported.ShouldNotBeNull();
        OmpModelsLayout.Analyze("providers:\n" + Fresh("acme", "\n") + Fresh("acme", "\n")).Unsupported.ShouldNotBeNull();
    }

    /// <summary>A legacy marker at the wrong indentation or without an end is unsupported like the new family.</summary>
    [Fact]
    public void MalformedLegacyBlocksAreUnsupportedLikeTheNewFamily()
    {
        var wrongIndent = "providers:\n  other: 1\n    " + LegacyBrandNames.LegacyBegin("acme") + "\n";
        var unclosed = "providers:\n  " + LegacyBrandNames.LegacyBegin("acme") + "\n    x: 1\n";
        var wrongIndentNew = "providers:\n  other: 1\n    " + NewBegin + "acme\n";
        var unclosedNew = "providers:\n  " + NewBegin + "acme\n    x: 1\n";

        OmpModelsLayout.Analyze(wrongIndent).Unsupported.ShouldBe(OmpModelsLayout.Analyze(wrongIndentNew).Unsupported);
        OmpModelsLayout.Analyze(unclosed).Unsupported.ShouldBe(OmpModelsLayout.Analyze(unclosedNew).Unsupported);
        OmpModelsLayout.Analyze(wrongIndent).Unsupported.ShouldNotBeNull();
        OmpModelsLayout.Analyze(unclosed).Unsupported.ShouldNotBeNull();
    }

    /// <summary>An end marker of the other family does not close a block.</summary>
    [Fact]
    public void AnEndMarkerOfTheOtherFamilyDoesNotCloseABlock()
    {
        var text = "providers:\n  " + LegacyBrandNames.LegacyBegin("acme") + "\n    x: 1\n  " + NewEnd + "acme\n";

        OmpModelsLayout.Analyze(text).Unsupported.ShouldNotBeNull();
    }

    // ---- editor ----

    /// <summary>Upsert over a legacy-only block rewrites it under the new markers at the same place.</summary>
    [Fact]
    public void UpsertRewritesALegacyBlockUnderTheNewMarkers()
    {
        var original = "# head\nproviders:\n  mine:\n    x: 1\n" + Legacy("acme", "\n") + "other: 1\n";

        var edit = OmpModelsEditor.Upsert(original, OmpModelsFixtures.Local());

        edit.Status.ShouldBe(OmpModelsWriteStatus.Written);
        edit.NewText.ShouldBe("# head\nproviders:\n  mine:\n    x: 1\n" + Fresh("acme", "\n") + "other: 1\n");
    }

    /// <summary>Upsert over a same-id twin keeps the new block and deletes the legacy span in the same write.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UpsertOverATwinDeletesTheLegacySpan(bool legacyFirst)
    {
        var legacy = Legacy("acme", "\n", "old");
        var fresh = Fresh("acme", "\n");
        var original = "providers:\n" + (legacyFirst ? legacy + fresh : fresh + legacy) + "other: 1\n";

        var edit = OmpModelsEditor.Upsert(original, OmpModelsFixtures.Local());

        edit.Status.ShouldBe(OmpModelsWriteStatus.Written);
        edit.NewText.ShouldBe("providers:\n" + fresh + "other: 1\n");
        OmpModelsEditor.ReadManaged(edit.NewText!).Providers.Count.ShouldBe(1);
    }

    /// <summary>Different ids in different families are updated in place, each normalized on its own.</summary>
    [Fact]
    public void UpsertDoesNotCrossTalkBetweenIds()
    {
        var original = "providers:\n" + Legacy("acme", "\n") + Fresh("beta", "\n");

        var first = OmpModelsEditor.Upsert(original, OmpModelsFixtures.Local()).NewText!;

        first.ShouldBe("providers:\n" + Fresh("acme", "\n") + Fresh("beta", "\n"));

        var legacyBeta = "providers:\n" + Fresh("acme", "\n") + Legacy("beta", "\n");
        var second = OmpModelsEditor.Upsert(legacyBeta, OmpModelsFixtures.Local("beta")).NewText!;

        second.ShouldBe("providers:\n" + Fresh("acme", "\n") + Fresh("beta", "\n"));
    }

    /// <summary>Removal deletes a legacy-only block, a new-only block, and both spans of a twin.</summary>
    [Theory]
    [InlineData("legacy")]
    [InlineData("new")]
    [InlineData("twin")]
    public void RemoveDeletesEveryFamilySpan(string shape)
    {
        var blocks = shape switch
        {
            "legacy" => Legacy("acme", "\n"),
            "new" => Fresh("acme", "\n"),
            _ => Legacy("acme", "\n", "old") + Fresh("acme", "\n"),
        };
        var kept = "# head\nproviders:\n  mine:\n    x: 1\n";

        var edit = OmpModelsEditor.Remove(kept + blocks + "other: 1\n", "acme");

        edit.Status.ShouldBe(OmpModelsWriteStatus.Written);
        edit.NewText.ShouldBe(kept + "other: 1\n");
    }

    /// <summary>Removing the last legacy block also removes a providers line Autonogrammer created.</summary>
    [Fact]
    public void RemoveDropsTheCreatedProvidersLine()
    {
        var original = "providers: # autonogrammer:created\n" + Legacy("acme", "\n") + Fresh("acme", "\n");

        var edit = OmpModelsEditor.Remove(original, "acme");

        edit.Status.ShouldBe(OmpModelsWriteStatus.Written);
        edit.NewText.ShouldBeEmpty();
    }

    /// <summary>CRLF, byte order mark, comments and a missing final newline survive a legacy upsert, a twin upsert and a legacy removal.</summary>
    [Fact]
    public void BytesOutsideTheEditedSpanArePreserved()
    {
        const string Crlf = "\r\n";
        var head = "\uFEFF# head" + Crlf + "providers:" + Crlf + "  # mine" + Crlf + "  keep:" + Crlf + "    x: 1 # note" + Crlf;
        var tail = "  # tail" + Crlf + "other: 1";

        var legacyText = head + Legacy("acme", Crlf) + tail;
        var twinText = head + Legacy("acme", Crlf, "old") + Fresh("acme", Crlf) + tail;

        OmpModelsEditor.Upsert(legacyText, OmpModelsFixtures.Local()).NewText.ShouldBe(head + Fresh("acme", Crlf) + tail);
        OmpModelsEditor.Upsert(twinText, OmpModelsFixtures.Local()).NewText.ShouldBe(head + Fresh("acme", Crlf) + tail);
        OmpModelsEditor.Remove(legacyText, "acme").NewText.ShouldBe(head + tail);
    }

    /// <summary>A legacy block that ends the file without a final newline stays without one after removal.</summary>
    [Fact]
    public void RemovingALegacyBlockAtTheEndKeepsTheMissingFinalNewline()
    {
        var original = "providers:\n  keep:\n    x: 1\n" + Legacy("acme", "\n").TrimEnd('\n');

        OmpModelsEditor.Remove(original, "acme").NewText.ShouldBe("providers:\n  keep:\n    x: 1");
    }

    /// <summary>Repeating a write after normalization is unchanged.</summary>
    [Fact]
    public void ASecondWriteIsUnchanged()
    {
        var original = "providers:\n" + Legacy("acme", "\n", "old") + Fresh("acme", "\n");

        var first = OmpModelsEditor.Upsert(original, OmpModelsFixtures.Local());
        var second = OmpModelsEditor.Upsert(first.NewText!, OmpModelsFixtures.Local());

        first.Status.ShouldBe(OmpModelsWriteStatus.Written);
        second.Status.ShouldBe(OmpModelsWriteStatus.Unchanged);
    }

    /// <summary>Reading a legacy block returns its provider.</summary>
    [Fact]
    public void ReadManagedReadsALegacyBlock()
    {
        var read = OmpModelsEditor.ReadManaged("providers:\n" + Legacy("acme", "\n"));

        read.Problems.ShouldBeEmpty();
        read.Providers.Select(provider => provider.Id).ShouldBe(["acme"]);
    }

    // ---- file level ----

    /// <summary>The file layer rewrites a legacy-only file under the new markers, keeps a backup and validates with the real command.</summary>
    [Fact]
    public async Task FileLevelUpsertNormalizesBacksUpAndValidates()
    {
        using var fake = new FakeOmpEnvironment();
        var original = "providers:\n" + Legacy("acme", "\n") + "other: 1\n";
        fake.WriteModelsYml(original);
        var file = new OmpModelsFile(fake.Options());

        var result = await file.UpsertAsync(OmpModelsFixtures.Local(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Status.ShouldBe(OmpModelsWriteStatus.Written);
        Encoding.UTF8.GetString(fake.ReadModelsYml()!).ShouldBe("providers:\n" + Fresh("acme", "\n") + "other: 1\n");
        fake.Backups().Length.ShouldBe(1);
        fake.Calls().Single().Arguments.ShouldBe(["models", "--json", "--no-extensions"]);
    }

    // ---- helpers ----

    private static string Block(string begin, string end, string eol, string? baseUrl)
    {
        var builder = new StringBuilder();
        _ = builder.Append("  ").Append(begin).Append(eol).Append("  \"acme\":").Append(eol);
        foreach (var line in BodyLines)
        {
            var text = baseUrl is not null && line.Contains("baseUrl", StringComparison.Ordinal)
                ? "    baseUrl: \"http://old.example/" + baseUrl + "\""
                : line;
            _ = builder.Append(text).Append(eol);
        }

        return builder.Append("  ").Append(end).Append(eol).ToString();
    }

    private static string Fresh(string id, string eol) => WithId(Block(NewBegin + id, NewEnd + id, eol, null), id);

    private static string Legacy(string id, string eol, string? baseUrl = null) =>
        WithId(Block(LegacyBrandNames.LegacyBegin(id), LegacyBrandNames.LegacyEnd(id), eol, baseUrl), id);

    private static string WithId(string block, string id) => block.Replace("\"acme\":", "\"" + id + "\":", StringComparison.Ordinal);
}
