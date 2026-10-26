using OmpOS.Presentation.Settings.Customize;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Settings.Customize;

/// <summary>The short description a head derives from a rule or skill file.</summary>
public sealed class CustomizeDescriptionTests
{
    /// <summary>The first line that is not a heading, a fence or blank is the description.</summary>
    [Fact]
    public void FirstProseLineWins()
    {
        CustomizeDescription.FromMarkdown("# Style\n\n```\ncode\n```\n\nUse file-scoped namespaces.\nSecond line.").ShouldBe("Use file-scoped namespaces.");
        CustomizeDescription.FromMarkdown("\uFEFF# Title\r\nProse here.\r\n").ShouldBe("Prose here.");
    }

    /// <summary>A description key in a front-matter block wins over the body.</summary>
    [Theory]
    [InlineData("---\nname: x\ndescription: Does a thing\n---\nBody text.", "Does a thing")]
    [InlineData("---\ndescription: \"Quoted value\"\n---\nBody text.", "Quoted value")]
    [InlineData("---\nname: x\n---\n# Heading\nBody text.", "Body text.")]
    [InlineData("---\ndescription:\n---\nBody text.", "Body text.")]
    public void FrontMatterDescriptionWins(string markdown, string expected)
    {
        CustomizeDescription.FromMarkdown(markdown).ShouldBe(expected);
    }

    /// <summary>Blank text, headings only and a fence that never closes give an empty description.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  ")]
    [InlineData("# Only a heading\n## Another")]
    [InlineData("```\nnever closed\nprose inside")]
    public void NoProseGivesNothing(string? markdown)
    {
        CustomizeDescription.FromMarkdown(markdown).ShouldBeEmpty();
    }

    /// <summary>A long line is cut to 160 characters and ends with three dots.</summary>
    [Fact]
    public void LongLinesAreShortened()
    {
        var text = CustomizeDescription.FromMarkdown(new string('x', 500));

        text.Length.ShouldBe(CustomizeDescription.MaxLength);
        text.ShouldEndWith("...", Case.Sensitive);
        CustomizeDescription.FromMarkdown(new string('x', 160)).Length.ShouldBe(160);
    }
}
