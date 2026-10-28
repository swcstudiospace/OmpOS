using OmpOS.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Safety;

/// <summary>The token grammar: five kinds, an argument between parentheses, and a reason for everything else, never an exception.</summary>
public sealed class PermissionTokenParserTests
{
    [Theory]
    [InlineData("Shell(git)", PermissionTokenKind.Shell, "git")]
    [InlineData("Shell(git status)", PermissionTokenKind.Shell, "git status")]
    [InlineData("Shell(curl:*)", PermissionTokenKind.Shell, "curl:*")]
    [InlineData("Read(src/**/*.ts)", PermissionTokenKind.Read, "src/**/*.ts")]
    [InlineData("Write(**/.env*)", PermissionTokenKind.Write, "**/.env*")]
    [InlineData("WebFetch(*.example.com)", PermissionTokenKind.WebFetch, "*.example.com")]
    [InlineData("Mcp(github:list_*)", PermissionTokenKind.Mcp, "github:list_*")]
    [InlineData("  shell(  git  )  ", PermissionTokenKind.Shell, "git")]
    [InlineData("Shell(echo (hi))", PermissionTokenKind.Shell, "echo (hi)")]
    public void AValidTokenParsesToItsKindAndArgument(string text, PermissionTokenKind kind, string argument)
    {
        var result = PermissionTokenParser.Parse(text);

        result.IsValid.ShouldBeTrue();
        result.Token.ShouldBe(new PermissionToken(kind, argument));
        result.Error.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Shell")]
    [InlineData("Shell(")]
    [InlineData("Shell()")]
    [InlineData("Shell(   )")]
    [InlineData("(git)")]
    [InlineData("Run(git)")]
    [InlineData("Mcp(github)")]
    [InlineData("Mcp(:tool)")]
    [InlineData("Mcp(server:)")]
    [InlineData("WebFetch(https://example.com/x)")]
    [InlineData("Shell(a\nb)")]
    public void AnInvalidTokenIsRejectedWithAReasonAndNeverThrows(string? text)
    {
        var result = PermissionTokenParser.Parse(text);

        result.IsValid.ShouldBeFalse();
        result.Token.ShouldBeNull();
        result.Error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ATokenLongerThanTheLimitIsRejected()
    {
        var result = PermissionTokenParser.Parse("Shell(" + new string('a', PermissionTokenParser.MaxTokenLength) + ")");

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldNotBeNull().ShouldContain("512", Case.Sensitive);
    }

    [Fact]
    public void TheTextOfATokenRoundTrips()
    {
        PermissionTokenParser.Parse("Read(src/**)").Token!.Text.ShouldBe("Read(src/**)");
    }
}
