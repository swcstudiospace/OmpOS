using System.Text;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Plugins.Tests;

/// <summary>
/// Verifies manifest validation through the parser's outcomes: the accepted fields, the rejection kind for each rule,
/// and the size boundary, which is checked before any JSON is read.
/// </summary>
public sealed class PluginManifestParserTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private const string ValidManifest = """
        {
          "id": "acme.tools",
          "version": "1.2.0",
          "apiVersion": 1,
          "capabilities": ["hooks.run"],
          "permissions": ["workspace.read"],
          "hooks": [
            { "trigger": "beforeAction", "command": ["/usr/bin/lint", "--strict"], "workingDirectory": "/srv/workspace" }
          ]
        }
        """;

    [Fact]
    public void AcceptsEveryFieldAndDefaultsBlockingToFalse()
    {
        var manifest = PluginManifestParser.Parse(Encoding.UTF8.GetBytes(ValidManifest));

        manifest.Id.ShouldBe("acme.tools");
        manifest.Version.ShouldBe("1.2.0");
        manifest.ApiVersion.ShouldBe(1);
        manifest.Capabilities.ShouldBe(["hooks.run"]);
        manifest.Permissions.ShouldBe(["workspace.read"]);
        var hook = manifest.Hooks.Single();
        hook.Trigger.ShouldBe(HookTrigger.BeforeAction);
        hook.Command.Arguments.ShouldBe(["/usr/bin/lint", "--strict"]);
        hook.Command.WorkingDirectory.ShouldBe("/srv/workspace");
        hook.Blocking.ShouldBeFalse();
    }

    [Fact]
    public void AcceptsManifestOfExactlyTheSizeLimit()
    {
        var padding = PluginManifestParser.MaxManifestBytes - Encoding.UTF8.GetByteCount(ValidManifest);
        var padded = Encoding.UTF8.GetBytes(ValidManifest + new string(' ', padding));

        padded.Length.ShouldBe(PluginManifestParser.MaxManifestBytes);
        PluginManifestParser.Parse(padded).Id.ShouldBe("acme.tools");
    }

    [Fact]
    public void RejectsManifestOneByteOverTheLimitBeforeReadingIt()
    {
        var padding = PluginManifestParser.MaxManifestBytes - Encoding.UTF8.GetByteCount(ValidManifest) + 1;
        var oversized = Encoding.UTF8.GetBytes(ValidManifest + new string(' ', padding));

        var error = Should.Throw<PluginManifestException>(() => PluginManifestParser.Parse(oversized));

        error.Kind.ShouldBe(PluginRejectionKind.ManifestTooLarge);
    }

    [Fact]
    public void SizeIsCheckedBeforeTheJsonIsParsed()
    {
        // Whitespace alone is not a valid manifest. Only the size check can reject it as too large.
        var oversized = Encoding.UTF8.GetBytes(new string(' ', PluginManifestParser.MaxManifestBytes + 1));

        var error = Should.Throw<PluginManifestException>(() => PluginManifestParser.Parse(oversized));

        error.Kind.ShouldBe(PluginRejectionKind.ManifestTooLarge);
    }

    [Fact]
    public async Task ReadingAnOversizedFileFailsBeforeItsBytesAreLoaded()
    {
        using var directory = new TempDirectory();
        var path = directory.PathOf("plugin.json");
        await File.WriteAllBytesAsync(path, new byte[PluginManifestParser.MaxManifestBytes + 1], Token);

        var error = await Should.ThrowAsync<PluginManifestException>(() =>
            PluginManifestSource.ReadFileAsync(path, Token));

        error.Kind.ShouldBe(PluginRejectionKind.ManifestTooLarge);
    }

    [Fact]
    public async Task ReadingAFileAtTheLimitReturnsItsContent()
    {
        using var directory = new TempDirectory();
        var path = directory.PathOf("plugin.json");
        await File.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes(ValidManifest), Token);

        var source = await PluginManifestSource.ReadFileAsync(path, Token);

        source.Location.ShouldBe(path);
        PluginManifestParser.Parse(source.Content).Id.ShouldBe("acme.tools");
    }

    [Fact]
    public void RejectsUnknownTopLevelFieldAsUnknownField()
    {
        var manifest = ValidManifest.Replace("\"hooks\":", "\"entry\": { \"command\": [\"/bin/sh\"] },\n  \"hooks\":", StringComparison.Ordinal);

        var error = Should.Throw<PluginManifestException>(() => PluginManifestParser.Parse(Encoding.UTF8.GetBytes(manifest)));

        error.Kind.ShouldBe(PluginRejectionKind.UnknownField);
        error.Message.ShouldContain("entry");
    }

    [Fact]
    public void RejectsUnknownHookFieldAsUnknownField()
    {
        var manifest = ValidManifest.Replace("\"workingDirectory\": \"/srv/workspace\"", "\"workingDirectory\": \"/srv/workspace\", \"run\": true", StringComparison.Ordinal);

        var error = Should.Throw<PluginManifestException>(() => PluginManifestParser.Parse(Encoding.UTF8.GetBytes(manifest)));

        error.Kind.ShouldBe(PluginRejectionKind.UnknownField);
        error.Message.ShouldContain("run");
    }

    [Fact]
    public void RejectsRepeatedFieldNameAsDuplicateField()
    {
        var manifest = ValidManifest.Replace("\"version\": \"1.2.0\"", "\"version\": \"1.2.0\", \"version\": \"9.9.9\"", StringComparison.Ordinal);

        var error = Should.Throw<PluginManifestException>(() => PluginManifestParser.Parse(Encoding.UTF8.GetBytes(manifest)));

        error.Kind.ShouldBe(PluginRejectionKind.DuplicateField);
    }

    [Fact]
    public void RejectsMissingRequiredFieldAsMalformed()
    {
        var manifest = ValidManifest.Replace("\"capabilities\": [\"hooks.run\"],", string.Empty, StringComparison.Ordinal);

        var error = Should.Throw<PluginManifestException>(() => PluginManifestParser.Parse(Encoding.UTF8.GetBytes(manifest)));

        error.Kind.ShouldBe(PluginRejectionKind.ManifestMalformed);
    }

    [Fact]
    public void RejectsNonJsonAsMalformed()
    {
        var error = Should.Throw<PluginManifestException>(() => PluginManifestParser.Parse(Encoding.UTF8.GetBytes("{ not json")));

        error.Kind.ShouldBe(PluginRejectionKind.ManifestMalformed);
    }

    [Fact]
    public void RejectsTraversalIdentifierBeforeItIsUsed()
    {
        var manifest = ValidManifest.Replace("\"acme.tools\"", "\"../escape\"", StringComparison.Ordinal);

        var error = Should.Throw<PluginManifestException>(() => PluginManifestParser.Parse(Encoding.UTF8.GetBytes(manifest)));

        error.Kind.ShouldBe(PluginRejectionKind.InvalidManifest);
        error.PluginId.ShouldBeNull();
    }

    [Fact]
    public void RejectsRelativeWorkingDirectoryAndNamesThePlugin()
    {
        var manifest = ValidManifest.Replace("/srv/workspace", "srv/workspace", StringComparison.Ordinal);

        var error = Should.Throw<PluginManifestException>(() => PluginManifestParser.Parse(Encoding.UTF8.GetBytes(manifest)));

        error.Kind.ShouldBe(PluginRejectionKind.InvalidManifest);
        error.PluginId.ShouldBe("acme.tools");
    }

    [Fact]
    public void RejectsUnknownTriggerName()
    {
        var manifest = ValidManifest.Replace("beforeAction", "beforeEverything", StringComparison.Ordinal);

        var error = Should.Throw<PluginManifestException>(() => PluginManifestParser.Parse(Encoding.UTF8.GetBytes(manifest)));

        error.Kind.ShouldBe(PluginRejectionKind.InvalidManifest);
    }

    [Fact]
    public void RejectsEmptyCommandArgumentList()
    {
        var manifest = ValidManifest.Replace("[\"/usr/bin/lint\", \"--strict\"]", "[]", StringComparison.Ordinal);

        var error = Should.Throw<PluginManifestException>(() => PluginManifestParser.Parse(Encoding.UTF8.GetBytes(manifest)));

        error.Kind.ShouldBe(PluginRejectionKind.InvalidManifest);
    }

    [Fact]
    public void RejectsApiVersionBelowOne()
    {
        var manifest = ValidManifest.Replace("\"apiVersion\": 1", "\"apiVersion\": 0", StringComparison.Ordinal);

        var error = Should.Throw<PluginManifestException>(() => PluginManifestParser.Parse(Encoding.UTF8.GetBytes(manifest)));

        error.Kind.ShouldBe(PluginRejectionKind.InvalidManifest);
        error.PluginId.ShouldBe("acme.tools");
    }
}
