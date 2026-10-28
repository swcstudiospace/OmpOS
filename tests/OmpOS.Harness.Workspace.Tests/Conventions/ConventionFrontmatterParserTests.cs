using OmpOS.Workspace.Conventions;
using Shouldly;
using Xunit;

namespace OmpOS.Workspace.Tests.Conventions;

/// <summary>The small, bounded frontmatter parser: the key forms conventions use, and the inputs it must refuse.</summary>
public sealed class ConventionFrontmatterParserTests
{
    /// <summary>Text with no frontmatter fence is all body, with no fields and no problem.</summary>
    [Fact]
    public void TextWithoutFrontmatterIsAllBody()
    {
        var parsed = ConventionFrontmatterParser.Parse("# Title\nBody line.\n");

        parsed.Fields.ShouldBeEmpty();
        parsed.Body.ShouldBe("# Title\nBody line.\n");
        parsed.Problem.ShouldBeNull();
    }

    /// <summary>A byte order mark is ignored and the body starts after the closing fence.</summary>
    [Fact]
    public void AByteOrderMarkIsIgnoredAndTheBodyFollowsTheFence()
    {
        var parsed = ConventionFrontmatterParser.Parse("\uFEFF---\ndescription: Hello\n---\nBody\n");

        parsed.Problem.ShouldBeNull();
        parsed.ReadText("description").ShouldBe("Hello");
        parsed.Body.ShouldBe("Body\n");
    }

    /// <summary>Plain, double-quoted and single-quoted scalars are read; a trailing comment is dropped from a plain scalar; a colon inside a value is kept; escapes in double quotes are decoded.</summary>
    [Fact]
    public void ScalarsAreReadInEveryForm()
    {
        var parsed = ConventionFrontmatterParser.Parse(
            "---\nplain: Use when: testing  # a comment\ndouble: \"say \\\"hi\\\"\\tnow\"\nsingle: 'it''s fine'\nempty:\nurl: https://example.com/a#b\n---\n");

        parsed.Problem.ShouldBeNull();
        parsed.ReadText("plain").ShouldBe("Use when: testing");
        parsed.ReadText("double").ShouldBe("say \"hi\"\tnow");
        parsed.ReadText("single").ShouldBe("it's fine");
        parsed.ReadText("empty").ShouldBeNull();
        parsed.ReadText("url").ShouldBe("https://example.com/a#b");
    }

    /// <summary>Folded (&gt;) and literal (|) block scalars join their indented lines with spaces and newlines, and a plain scalar continues on indented lines.</summary>
    [Fact]
    public void BlockAndContinuedScalarsAreJoined()
    {
        var parsed = ConventionFrontmatterParser.Parse(
            "---\nfolded: >\n  first line\n  second line\nliteral: |\n  one\n  two\nwrapped: starts here\n  and goes on\n---\n");

        parsed.Problem.ShouldBeNull();
        parsed.ReadText("folded").ShouldBe("first line second line");
        parsed.ReadText("literal").ShouldBe("one\ntwo");
        parsed.ReadText("wrapped").ShouldBe("starts here and goes on");
    }

    /// <summary>Flow lists, block lists and comma-separated scalars all read as lists; a quoted comma stays inside its item.</summary>
    [Fact]
    public void ListsAreReadInEveryForm()
    {
        var parsed = ConventionFrontmatterParser.Parse(
            "---\nflow: [a, 'b, c', \"d\"]\nblock:\n  - src/**\n  - \"docs/*.md\"\ncomma: **/*.ts, **/*.tsx\nglob: *.md\n---\n");

        parsed.Problem.ShouldBeNull();
        parsed.ReadList("flow").ShouldBe(["a", "b, c", "d"]);
        parsed.ReadList("block").ShouldBe(["src/**", "docs/*.md"]);
        parsed.ReadList("comma").ShouldBe(["**/*.ts", "**/*.tsx"]);
        parsed.ReadList("glob").ShouldBe(["*.md"], "a value that starts with an asterisk is text, never an alias");
        parsed.ReadList("missing").ShouldBeEmpty();
    }

    /// <summary>Yes-or-no keys read in any case; other text is not a flag.</summary>
    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("yes", true)]
    [InlineData("false", false)]
    [InlineData("Off", false)]
    [InlineData("maybe", null)]
    public void FlagsReadInAnyCase(string value, bool? expected)
    {
        var parsed = ConventionFrontmatterParser.Parse("---\nflag: " + value + "\n---\n");

        parsed.ReadFlag("flag").ShouldBe(expected);
    }

    /// <summary>A nested map is skipped without error, and the keys around it are still read.</summary>
    [Fact]
    public void ANestedMapIsSkipped()
    {
        var parsed = ConventionFrontmatterParser.Parse("---\nname: x\nmetadata:\n  author: me\n  version: 2\ninline: { a: 1 }\ndescription: d\n---\n");

        parsed.Problem.ShouldBeNull();
        parsed.Fields.Keys.OrderBy(key => key, StringComparer.Ordinal).ShouldBe(["description", "name"]);
    }

    /// <summary>Windows line endings are read like Unix ones.</summary>
    [Fact]
    public void WindowsLineEndingsAreRead()
    {
        var parsed = ConventionFrontmatterParser.Parse("---\r\nname: x\r\ndescription: d\r\n---\r\nBody\r\n");

        parsed.Problem.ShouldBeNull();
        parsed.ReadText("name").ShouldBe("x");
        parsed.ReadText("description").ShouldBe("d");
    }

    /// <summary>YAML anchors, aliases, tags and merge keys are never interpreted: their text is kept as written.</summary>
    [Fact]
    public void AnchorsAliasesAndTagsAreNotInterpreted()
    {
        var parsed = ConventionFrontmatterParser.Parse("---\na: &anchor value\nb: *anchor\nc: !!str 12\n---\n");

        parsed.Problem.ShouldBeNull();
        parsed.ReadText("a").ShouldBe("&anchor value");
        parsed.ReadText("b").ShouldBe("*anchor");
        parsed.ReadText("c").ShouldBe("!!str 12");
    }

    /// <summary>A block that is not closed, a line that is not a key, a repeated key, an unclosed list and an orphan indented line are problems with no fields.</summary>
    [Theory]
    [InlineData("---\nname: x\nno closing fence\n", "not closed")]
    [InlineData("---\nthis is not a key\n---\n", "not 'key: value'")]
    [InlineData("---\nname: x\nname: y\n---\n", "appears twice")]
    [InlineData("---\nlist: [a, b\n---\n", "not closed with ']'")]
    [InlineData("---\n  orphan: line\n---\n", "no key above it")]
    [InlineData("---\nbad key: x\n---\n", "not 'key: value'")]
    public void MalformedFrontmatterIsAProblem(string text, string reasonPart)
    {
        var parsed = ConventionFrontmatterParser.Parse(text);

        parsed.Problem.ShouldNotBeNull().ShouldContain(reasonPart, Case.Sensitive);
        parsed.Fields.ShouldBeEmpty();
        parsed.Body.ShouldBe(text, "a malformed block leaves the whole text as the body");
    }

    /// <summary>The block is bounded: more than 200 lines, more than 100 keys and a line over 4096 characters are problems, not hangs.</summary>
    [Fact]
    public void TheBlockIsBounded()
    {
        var tooLong = "---\n" + string.Concat(Enumerable.Repeat("k: v\n", ConventionFrontmatterParser.MaxLines + 5)) + "---\n";
        var tooManyKeys = "---\n" + string.Concat(Enumerable.Range(0, 101).Select(index => "k" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ": v\n")) + "---\n";
        var longLine = "---\nk: " + new string('x', 5000) + "\n---\n";

        ConventionFrontmatterParser.Parse(tooLong).Problem.ShouldNotBeNull();
        ConventionFrontmatterParser.Parse(tooManyKeys).Problem.ShouldNotBeNull().ShouldContain("too many keys", Case.Sensitive);
        ConventionFrontmatterParser.Parse(longLine).Problem.ShouldNotBeNull().ShouldContain("too long", Case.Sensitive);
    }

    /// <summary>Null input is refused.</summary>
    [Fact]
    public void NullTextIsRefused()
    {
        Should.Throw<ArgumentNullException>(() => ConventionFrontmatterParser.Parse(null!));
    }
}
