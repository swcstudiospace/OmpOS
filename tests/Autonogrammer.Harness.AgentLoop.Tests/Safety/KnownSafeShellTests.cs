using Autonogrammer.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Safety;

/// <summary>The public known-safe facade reads the internal list without InternalsVisibleTo on Cowork.</summary>
public sealed class KnownSafeShellTests
{
    [Fact]
    public void AShortReadOnlyCommandIsKnownSafeWhenArgumentsAreAllowed()
    {
        KnownSafeShell.IsKnownSafe("ls notes.txt", static argument => string.Equals(argument, "notes.txt", StringComparison.Ordinal)).ShouldBeTrue();
        KnownSafeShell.IsKnownSafe("pwd", static _ => false).ShouldBeTrue();
    }

    [Fact]
    public void AnArgumentOutsideTheBoundaryIsNotKnownSafe()
    {
        KnownSafeShell.IsKnownSafe("ls /elsewhere", static _ => false).ShouldBeFalse();
        KnownSafeShell.IsKnownSafe("rm notes.txt", static _ => true).ShouldBeFalse();
    }

    [Fact]
    public void EverySegmentMatchingAnAllowPatternIsAllowed()
    {
        KnownSafeShell.AllSegmentsMatchShellRules("ls && ls", ["ls"]).ShouldBeTrue();
        KnownSafeShell.AllSegmentsMatchShellRules("ls; git status", ["ls", "git status"]).ShouldBeTrue();
    }

    [Fact]
    public void OneSegmentMatchingNoPatternIsNotAllowed()
    {
        KnownSafeShell.AllSegmentsMatchShellRules("ls; git push --force", ["ls"]).ShouldBeFalse();
        KnownSafeShell.AllSegmentsMatchShellRules("ls | sh", ["ls"]).ShouldBeFalse();
    }

    [Fact]
    public void AnOpaqueLineIsNotAllowedEvenWhenItsWordsMatch()
    {
        KnownSafeShell.AllSegmentsMatchShellRules("ls > out.txt", ["ls"]).ShouldBeFalse();
    }

    [Theory]
    [InlineData("cat .e*")]
    [InlineData("cat .[e]nv")]
    [InlineData("cat {.env,README.md}")]
    [InlineData("grep -f.e* README.md")]
    [InlineData("grep --file=.e* README.md")]
    [InlineData("cat ~root/.env")]
    public void AnUnexpandedWordIsNotKnownSafeEvenWhenTheCallbackAllowsIt(string commandLine)
    {
        KnownSafeShell.IsKnownSafe(commandLine, static _ => true).ShouldBeFalse();
    }
}
