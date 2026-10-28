using Autonogrammer.Api.Services.WorkspaceSettings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.WorkspaceSettings;

/// <summary>What <see cref="PathRedactor"/> hides and shows.</summary>
public sealed class PathRedactorTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "srv", "ws");
    private static readonly string Home = Path.Combine(Path.GetTempPath(), "home", "ada");

    /// <summary>The workspace folder and the home directory are replaced inside a sentence; other text is untouched.</summary>
    [Fact]
    public void Redact_ReplacesTheWorkspaceAndTheHome()
    {
        var sentence = $"the folder cannot be read: access to '{Path.Combine(Root, ".cursor")}' and '{Path.Combine(Home, ".claude")}' is denied";

        var redacted = PathRedactor.Redact(sentence, Root, Home);

        redacted.ShouldBe($"the folder cannot be read: access to '<workspace>{Path.DirectorySeparatorChar}.cursor' and '~{Path.DirectorySeparatorChar}.claude' is denied");
    }

    /// <summary>A workspace inside the home is hidden as the workspace, not cut short by the home replacement.</summary>
    [Fact]
    public void Redact_PrefersTheLongerPath()
    {
        var workspace = Path.Combine(Home, "projects", "app");

        var redacted = PathRedactor.Redact($"cannot read {workspace}", workspace, Home);

        redacted.ShouldBe("cannot read <workspace>");
    }

    /// <summary>With no folder and no home the sentence is returned as it is.</summary>
    [Fact]
    public void Redact_WithNothingToHide_ReturnsTheSentence()
    {
        PathRedactor.Redact("plain sentence", null, null).ShouldBe("plain sentence");
    }

    /// <summary>A path is labelled relative to the workspace, with a <c>~</c> prefix below the home, or by its file name only.</summary>
    [Fact]
    public void Label_ShowsRelativePathsOnly()
    {
        PathRedactor.Label(Path.Combine(Root, ".cursor", "hooks.json"), Root, Home).ShouldBe(".cursor/hooks.json");
        PathRedactor.Label(Root, Root, Home).ShouldBe(".");
        PathRedactor.Label(Path.Combine(Home, ".cursor", "hooks.json"), Root, Home).ShouldBe("~/.cursor/hooks.json");
        PathRedactor.Label(Home, Root, Home).ShouldBe("~");
        PathRedactor.Label(Path.Combine(Path.GetTempPath(), "elsewhere", "hooks.json"), Root, Home).ShouldBe("<elsewhere>/hooks.json");
    }

    /// <summary>A directory that merely starts with the workspace name is not inside it.</summary>
    [Fact]
    public void Label_DoesNotTreatASiblingPrefixAsInside()
    {
        var sibling = Root + "-other";

        PathRedactor.Label(Path.Combine(sibling, "x.json"), Root, Home).ShouldBe("<elsewhere>/x.json");
    }
}
