using Autonogrammer.Presentation.Commands;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Commands;

/// <summary>Splitting typed arguments and filling a command template: the same placeholders omp uses for its own file commands.</summary>
public sealed class SlashCommandArgumentsTests
{
    /// <summary>White space separates arguments, and a quoted run stays one argument without its quotes.</summary>
    [Theory]
    [InlineData("", new string[0])]
    [InlineData("   ", new string[0])]
    [InlineData("one two  three", new[] { "one", "two", "three" })]
    [InlineData("say \"hello world\" now", new[] { "say", "hello world", "now" })]
    [InlineData("'a b' c", new[] { "a b", "c" })]
    [InlineData("\"\" x", new[] { "", "x" })]
    [InlineData("unclosed \"quote here", new[] { "unclosed", "quote here" })]
    public void ArgumentsAreSplitOnWhiteSpaceAndQuotes(string text, string[] expected) =>
        SlashCommandArguments.Split(text).ShouldBe(expected);

    /// <summary>Each placeholder form is replaced: every argument, one argument, a slice from an argument, and a slice of a given length.</summary>
    [Theory]
    [InlineData("All: $ARGUMENTS", "a b c", "All: a b c")]
    [InlineData("All: $@", "a b c", "All: a b c")]
    [InlineData("$1-$2-$3", "a b c", "a-b-c")]
    [InlineData("$3 $1", "a b c", "c a")]
    [InlineData("Rest: $@[2]", "a b c", "Rest: b c")]
    [InlineData("Two: $@[1:2]", "a b c", "Two: a b")]
    [InlineData("Open: $@[2:]", "a b c", "Open: b c")]
    [InlineData("Zero: [$@[1:0]]", "a b c", "Zero: []")]
    [InlineData("Past: [$@[9]]", "a b c", "Past: []")]
    [InlineData("Missing: [$4]", "a b c", "Missing: []")]
    [InlineData("Quoted: $1", "\"a b\" c", "Quoted: a b")]
    public void PlaceholdersAreReplaced(string template, string typed, string expected) =>
        SlashCommandArguments.Expand(template, typed).ShouldBe(expected);

    /// <summary>A dollar sign that starts no placeholder stays as written, and a huge number is not a placeholder.</summary>
    [Theory]
    [InlineData("Cost: $5.00 or $x or $", "", "Cost: .00 or $x or $")]
    [InlineData("Price $", "x", "Price $\n\nx")]
    [InlineData("Id $12345678901 ok", "x", "Id $12345678901 ok\n\nx")]
    [InlineData("Slice $@[1: bad", "a", "Slice a[1: bad")]
    public void ADollarThatIsNotAPlaceholderIsKept(string template, string typed, string expected) =>
        SlashCommandArguments.Expand(template, typed).ShouldBe(expected);

    /// <summary>A template with no placeholder gets the typed text after a blank line; with nothing typed, the template is returned as it is.</summary>
    [Fact]
    public void TypedTextIsAppendedWhenTheTemplateHasNoPlaceholder()
    {
        SlashCommandArguments.Expand("Review the change.\n", "focus on tests").ShouldBe("Review the change.\n\nfocus on tests");
        SlashCommandArguments.Expand("Review the change.\n", "  ").ShouldBe("Review the change.\n");
    }

    /// <summary>Null input is refused.</summary>
    [Fact]
    public void NullArgumentsAreRefused()
    {
        Should.Throw<ArgumentNullException>(() => SlashCommandArguments.Split(null!));
        Should.Throw<ArgumentNullException>(() => SlashCommandArguments.Expand(null!, "x"));
        Should.Throw<ArgumentNullException>(() => SlashCommandArguments.Expand("x", null!));
    }
}
