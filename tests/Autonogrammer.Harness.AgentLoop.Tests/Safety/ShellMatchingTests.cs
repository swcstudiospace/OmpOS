using Autonogrammer.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Safety;

/// <summary>Matching a Shell token or a terminal allowlist entry against a command, and reading a command line conservatively.</summary>
public sealed class ShellMatchingTests
{
    private static string[] Words(string command) => [.. command.Split(' ', StringSplitOptions.RemoveEmptyEntries)];

    [Theory]
    [InlineData("git", "git status", true)]
    [InlineData("git", "gitk", false)]
    [InlineData("git", "git", true)]
    [InlineData("git status", "git status", true)]
    [InlineData("git status", "git status -s", true)]
    [InlineData("git status", "git stash", false)]
    [InlineData("git status", "git", false)]
    [InlineData("npm:install*", "npm install express", true)]
    [InlineData("npm:install*", "npm run build", false)]
    [InlineData("curl:*", "curl https://example.com", true)]
    [InlineData("curl:*", "curl", true)]
    [InlineData("curl:*", "curly x", false)]
    [InlineData("npm:run *", "npm run build", true)]
    [InlineData("Git", "git status", false)]
    [InlineData("make*", "make build", true)]
    [InlineData("./scripts/build.sh", "./scripts/build.sh --fast", true)]
    public void AnEntryMatchesByWordPrefixOrByBaseAndArguments(string pattern, string command, bool expected)
    {
        PermissionMatcher.MatchesShell(pattern, Words(command), forDeny: false).ShouldBe(expected);
    }

    [Fact]
    public void ADenyEntryAlsoMatchesTheCommandWithoutItsFolder()
    {
        PermissionMatcher.MatchesShell("rm", Words("/bin/rm -rf x"), forDeny: true).ShouldBeTrue();
        PermissionMatcher.MatchesShell("rm", Words("/bin/rm -rf x"), forDeny: false).ShouldBeFalse();
    }

    [Fact]
    public void ASimpleLineIsReadAsItsSegments()
    {
        var reading = ShellCommandAnalysis.ReadForAllow("git status && npm run \"build all\" ; ls | head -n 5");

        reading.Opaque.ShouldBeFalse();
        reading.Segments.Select(words => string.Join('|', words)).ToArray().ShouldBe(["git|status", "npm|run|build all", "ls", "head|-n|5"]);
    }

    [Theory]
    [InlineData("echo hi > file")]
    [InlineData("cat < file")]
    [InlineData("echo $(whoami)")]
    [InlineData("echo `whoami`")]
    [InlineData("(cd x && ls)")]
    [InlineData("sleep 1 &")]
    [InlineData("echo \"unterminated")]
    [InlineData("echo \"$(rm x)\"")]
    public void ALineWithSomethingUnreadableIsOpaque(string command)
    {
        ShellCommandAnalysis.ReadForAllow(command).Opaque.ShouldBeTrue();
    }

    [Fact]
    public void SingleQuotesHideEverythingInsideThem()
    {
        var reading = ShellCommandAnalysis.ReadForAllow("echo '$(rm x) > y; z'");

        reading.Opaque.ShouldBeFalse();
        reading.Segments.Single().ShouldBe(["echo", "$(rm x) > y; z"]);
    }

    [Theory]
    [InlineData("rm -rf x", "rm")]
    [InlineData("sudo rm -rf x", "rm")]
    [InlineData("sudo -u bob rm x", "rm")]
    [InlineData("env FOO=1 rm x", "rm")]
    [InlineData("FOO=1 rm x", "rm")]
    [InlineData("nohup timeout 5 rm x", "rm")]
    [InlineData("echo $(rm x)", "rm")]
    [InlineData("echo `rm x`", "rm")]
    [InlineData("ls && (rm x)", "rm")]
    [InlineData("bash -c 'rm x'", "rm")]
    [InlineData("sh -lc \"ls; rm x\"", "rm")]
    [InlineData("xargs rm", "rm")]
    public void EveryCommandALineMayRunIsACandidateForDeny(string command, string expectedWord)
    {
        ShellCommandAnalysis.CandidatesForDeny(command).Any(words => string.Equals(words[0], expectedWord, StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Fact]
    public void ADenyCandidateListDoesNotInventCommands()
    {
        ShellCommandAnalysis.CandidatesForDeny("echo rm").Select(words => words[0]).ToArray().ShouldBe(["echo"]);
    }

    [Theory]
    [InlineData("CI=1", true)]
    [InlineData("_X9=a", true)]
    [InlineData("1X=a", false)]
    [InlineData("a-b=c", false)]
    [InlineData("=x", false)]
    [InlineData("plain", false)]
    public void AnAssignmentWordIsRecognised(string word, bool expected)
    {
        ShellCommandAnalysis.IsAssignment(word).ShouldBe(expected);
    }

    [Theory]
    [InlineData("http://docs.example.com/a/b?q=1", "docs.example.com")]
    [InlineData("https://DOCS.Example.com:8443/", "docs.example.com")]
    [InlineData("docs.example.com", "docs.example.com")]
    [InlineData("example.com/path", "example.com")]
    public void AHostIsReadFromAnAddressOrABareHost(string address, string host)
    {
        PermissionMatcher.HostOf(address).ShouldBe(host);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoHostIsReadFromNothing(string? address)
    {
        PermissionMatcher.HostOf(address).ShouldBeNull();
    }

    [Theory]
    [InlineData("github:*", "github", "list_issues", true)]
    [InlineData("GitHub:List_*", "github", "list_issues", true)]
    [InlineData("*:search", "docs", "search", true)]
    [InlineData("*:search", "docs", "searching", false)]
    [InlineData("*:*", "any", "tool", true)]
    [InlineData("github:*", "gitlab", "x", false)]
    [InlineData("github", "github", "x", false)]
    public void AnMcpEntryMatchesServerAndToolIgnoringCase(string pattern, string server, string tool, bool expected)
    {
        PermissionMatcher.MatchesMcp(pattern, server, tool).ShouldBe(expected);
    }

    [Theory]
    [InlineData("mcp__github_list_issues", true, "github", "list_issues")]
    [InlineData("mcp__github__list_issues", true, "github", "list_issues")]
    [InlineData("mcp__server__a__b", true, "server", "a__b")]
    [InlineData("mcp__only", false, "", "")]
    [InlineData("mcp___tool", false, "", "")]
    [InlineData("mcp__srv_", false, "", "")]
    [InlineData("read_file", false, "", "")]
    public void AnMcpToolNameIsSplit(string name, bool parsed, string server, string tool)
    {
        McpToolName.TryParse(name, out var actualServer, out var actualTool).ShouldBe(parsed);
        actualServer.ShouldBe(server);
        actualTool.ShouldBe(tool);
    }
}
