using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>Quoting is tested the only way that proves it: the quoted word is handed to a real <c>sh</c> and must come back unchanged.</summary>
public sealed class PosixQuotingTests
{
    /// <summary>Hostile values come back byte for byte, and nothing in them runs.</summary>
    [Theory]
    [InlineData("plain")]
    [InlineData("with space")]
    [InlineData("it's")]
    [InlineData("a'b'c")]
    [InlineData("'")]
    [InlineData("''")]
    [InlineData("$(touch should-not-exist)")]
    [InlineData("`touch should-not-exist`")]
    [InlineData("$HOME")]
    [InlineData("${HOME}")]
    [InlineData("a;touch should-not-exist")]
    [InlineData("a && touch should-not-exist")]
    [InlineData("a | cat")]
    [InlineData("*")]
    [InlineData("~")]
    [InlineData("\\")]
    [InlineData("\\'")]
    [InlineData("\"")]
    [InlineData("-n")]
    [InlineData("--")]
    [InlineData("#comment")]
    [InlineData("!history")]
    [InlineData("tab\there")]
    [InlineData("ünïcödé 日本語")]
    public async Task HostileValuesRoundTripThroughASh(string value)
    {
        var ct = TestContext.Current.CancellationToken;
        using var directory = TempDirectory.Create();
        var script = "cd " + PosixQuoting.Quote(directory.Path) + " && printf '%s' " + PosixQuoting.Quote(value);

        var result = await ShellRunner.RunShAsync(script, null, null, ct).ConfigureAwait(true);

        result.ExitCode.ShouldBe(0);
        result.StandardOutput.ShouldBe(value);
        File.Exists(directory.Combine("should-not-exist")).ShouldBeFalse();
    }

    /// <summary>The empty string becomes an empty word, not nothing.</summary>
    [Fact]
    public void TheEmptyStringIsAnEmptyWord()
    {
        PosixQuoting.Quote(string.Empty).ShouldBe("''");
    }

    /// <summary>Every single quote in the value is closed, escaped and reopened.</summary>
    [Fact]
    public void SingleQuotesAreEscaped()
    {
        PosixQuoting.Quote("a'b").ShouldBe("'a'\\''b'");
    }

    /// <summary>A command line is one line: a line break or NUL is refused.</summary>
    [Theory]
    [InlineData("a\nb")]
    [InlineData("a\rb")]
    [InlineData("a\0b")]
    public void ALineBreakOrNulIsRefusedInACommandLine(string value)
    {
        Should.Throw<ArgumentException>(() => PosixQuoting.Quote(value));
    }

    /// <summary>The file-body quoting keeps a line break inside the quotes (the shell reads it back as the same text) but still refuses NUL.</summary>
    [Fact]
    public async Task AMultiLineValueRoundTripsInTheFileBodyForm()
    {
        var ct = TestContext.Current.CancellationToken;
        const string Value = "line one\nline 'two'\n$(id)";

        var result = await ShellRunner.RunShAsync("printf '%s' " + PosixQuoting.QuoteValue(Value), null, null, ct).ConfigureAwait(true);

        result.StandardOutput.ShouldBe(Value);
        Should.Throw<ArgumentException>(() => PosixQuoting.QuoteValue("a\0b"));
    }
}
