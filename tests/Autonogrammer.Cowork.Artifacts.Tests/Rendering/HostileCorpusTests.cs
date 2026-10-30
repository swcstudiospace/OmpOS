using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Cowork.Artifacts.Model;
using Autonogrammer.Cowork.Artifacts.Rendering;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Artifacts.Tests.Rendering;

/// <summary>Every hostile fixture has an asserted outcome and is named by a test.</summary>
public sealed class HostileCorpusTests
{
    [Fact]
    public void EveryHostileFixtureHasAnAssertedOutcome()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "hostile");
        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
        files.Length.ShouldBeGreaterThan(0);
        var sources = TestSources();
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            sources.Any(source => source.Contains(name, StringComparison.Ordinal)).ShouldBeTrue($"unnamed fixture {name}");
            AssertOutcome(file, name);
        }
    }

    [Fact]
    public async Task BidiTitleIsRefusedAsTitleInvalid()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "hostile", "md", "bidi-title.md");
        var title = (await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true)).TrimEnd('\r', '\n');
        title.Contains('\u202E', StringComparison.Ordinal).ShouldBeTrue();
        var service = new ArtifactService(new InMemoryArtifactStore(), TimeProvider.System, Options.Create(new ArtifactOptions()));
        var created = await service.CreateAsync(ArtifactKind.Markdown, title, "x"u8.ToArray(), null, null, null, null, ArtifactAuthor.Agent, TestContext.Current.CancellationToken).ConfigureAwait(true);
        created.Failure.ShouldNotBeNull().Code.ShouldBe("title_invalid");
    }

    private static void AssertOutcome(string file, string name)
    {
        var text = File.ReadAllText(file);
        switch (name)
        {
            case "doctype.svg":
                SvgSanitizer.Sanitize(text).Accepted.ShouldBeFalse();
                break;
            case "entity-bomb.svg":
                var bomb = SvgSanitizer.Sanitize(text);
                bomb.Accepted.ShouldBeFalse();
                bomb.Source.ShouldNotContain(new string('a', 20));
                break;
            case "foreign-object.svg":
                Document(text).ShouldNotContain("foreignObject");
                break;
            case "event-handler.svg":
                Document(text).ShouldNotContain("onload");
                break;
            case "external-ref.svg":
                Document(text).ShouldNotContain("https://evil.example");
                break;
            case "css-import.svg":
                Document(text).ShouldNotContain("@import");
                break;
            case "script-element.svg":
                Document(text).ShouldNotContain("<script");
                break;
            case "formula-cells.csv":
                CsvReader.Read(File.ReadAllBytes(file)).Rows[0][0].FormulaLike.ShouldBeTrue();
                break;
            case "oversized-cell.csv":
                CsvReader.Read(File.ReadAllBytes(file)).Code.ShouldBe("cell_too_long");
                break;
            case "fake-png.bin":
                ImageValidator.Validate(File.ReadAllBytes(file), "image/png", new ArtifactOptions().MaxImageBytes).Code.ShouldBe("magic_mismatch");
                break;
            case "oversized-dims.bin":
                ImageValidator.Validate(File.ReadAllBytes(file), "image/png", new ArtifactOptions().MaxImageBytes).Code.ShouldBe("dimensions");
                break;
            case "bidi-title.md":
                text.Contains('\u202E', StringComparison.Ordinal).ShouldBeTrue();
                ArtifactService.IsTitleValid(text.TrimEnd('\r', '\n')).ShouldBeFalse();
                break;
            default:
                AssertDocument(name, text);
                break;
        }
    }
    private static void AssertDocument(string name, string text)
    {
        switch (name)
        {
            case "meta-refresh.html":
                Wrapped(text).ShouldContain("base-uri 'none'");
                Wrapped(text).IndexOf("Content-Security-Policy", StringComparison.Ordinal).ShouldBeLessThan(Wrapped(text).IndexOf("refresh", StringComparison.Ordinal));
                break;
            case "base-tag.html":
                Wrapped(text).ShouldContain("base-uri 'none'");
                break;
            case "form-submit.html":
                Wrapped(text).ShouldContain("form-action 'none'");
                break;
            case "window-open.html":
                HtmlDocumentWrapper.DialogNoOpPrefix.ShouldNotContain("window.open(");
                Wrapped(text).ShouldStartWith("<meta http-equiv=\"Content-Security-Policy\"");
                break;
            case "location-assign.html":
                Wrapped(text).ShouldContain("script-src 'unsafe-inline'");
                break;
            case "postmessage-probe.html":
                HtmlDocumentWrapper.DialogNoOpPrefix.ShouldNotContain("postMessage");
                Wrapped(text).ShouldNotContain("addEventListener(\"message\"");
                break;
            case "bridge-probe.html":
                HtmlDocumentWrapper.DialogNoOpPrefix.ShouldNotContain("invokeCSharpAction");
                Wrapped(text).ShouldStartWith("<meta");
                break;
            case "init-directive.mmd":
                var html = MermaidDocumentBuilder.Build(text, new PresentAssets()).Html.ShouldNotBeNull();
                html.ShouldContain("securityLevel:'strict'");
                html.ShouldNotContain("securityLevel:\"loose\"");
                MermaidDocumentBuilder.Build(text, new AbsentAssets()).Reason.ShouldBe("MermaidAssetMissing");
                break;
            default:
                throw new InvalidOperationException("No asserted outcome for " + name);
        }
    }


    private static string Document(string text) => SvgSanitizer.Sanitize(text).Document ?? string.Empty;

    private static string Wrapped(string html) => HtmlDocumentWrapper.Wrap(html, scripts: true);

    private static List<string> TestSources()
    {
        var project = new DirectoryInfo(AppContext.BaseDirectory);
        while (project is not null && !File.Exists(Path.Combine(project.FullName, "Autonogrammer.Cowork.Artifacts.Tests.csproj")))
        {
            project = project.Parent;
        }

        project.ShouldNotBeNull();
        var repo = project.Parent!.Parent!;
        var roots = new[]
        {
            project.FullName,
            Path.Combine(repo.FullName, "tests", "Autonogrammer.UI.Tests", "Cowork", "Artifacts"),
            Path.Combine(repo.FullName, "tests", "Autonogrammer.Native.Tests"),
        };
        var sources = new List<string>();
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var source in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                sources.Add(File.ReadAllText(source));
            }
        }

        return sources;
    }

    private sealed class PresentAssets : IMermaidAssetProvider
    {
        public string? GetScript() => "/* mermaid */";
    }

    private sealed class AbsentAssets : IMermaidAssetProvider
    {
        public string? GetScript() => null;
    }
}
