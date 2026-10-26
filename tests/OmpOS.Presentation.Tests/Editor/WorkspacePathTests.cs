using OmpOS.Presentation.Editor;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>The workspace path rules that keep every listing and tab inside the root (ADE-10).</summary>
public sealed class WorkspacePathTests
{
    /// <summary>Separators, empty and dot segments are folded, and <c>..</c> resolves inside the root.</summary>
    [Theory]
    [InlineData("src//a/./b/../c.cs", "src/a/c.cs")]
    [InlineData("src\\models\\Item.cs", "src/models/Item.cs")]
    [InlineData("", "")]
    [InlineData("./", "")]
    [InlineData("a/..", "")]
    public void Normalize_FoldsSegmentsWithinTheRoot(string input, string expected)
    {
        WorkspacePath.Normalize(input).ShouldBe(expected);
    }

    /// <summary>A path that climbs above the root, or is rooted, is refused.</summary>
    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("src/../../secret.txt")]
    [InlineData("..")]
    [InlineData("/etc/passwd")]
    [InlineData("\\\\server\\share")]
    public void Normalize_RefusesPathsThatEscapeTheRoot(string input)
    {
        Should.Throw<ArgumentException>(() => WorkspacePath.Normalize(input));
    }

    /// <summary>A NUL character is refused, because it cannot appear in a path.</summary>
    [Fact]
    public void Normalize_RefusesNul()
    {
        Should.Throw<ArgumentException>(() => WorkspacePath.Normalize("a\0b"));
    }

    /// <summary>Only a single real segment is a plain name.</summary>
    [Theory]
    [InlineData("Program.cs", true)]
    [InlineData("", false)]
    [InlineData(".", false)]
    [InlineData("..", false)]
    [InlineData("a/b", false)]
    [InlineData("a\\b", false)]
    public void IsPlainName_AcceptsOnlyASingleSegment(string name, bool expected)
    {
        WorkspacePath.IsPlainName(name).ShouldBe(expected);
    }

    /// <summary>Combining a parent with a name gives the root-relative name for the root and a slash path below it.</summary>
    [Fact]
    public void Combine_JoinsWithASlashAndOmitsTheRootPrefix()
    {
        WorkspacePath.Combine(string.Empty, "README.md").ShouldBe("README.md");
        WorkspacePath.Combine("src/models", "Item.cs").ShouldBe("src/models/Item.cs");
    }
}
