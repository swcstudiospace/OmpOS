using Autonogrammer.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Safety;

/// <summary>Deletion commands ask. A deletion built in a script is the named gap <see cref="ShellDeleteDetector.ScriptDeletionGap"/>.</summary>
public sealed class ShellDeleteDetectorTests
{
    [Theory]
    [InlineData("rm notes.txt")]
    [InlineData("rmdir empty")]
    [InlineData("unlink notes.txt")]
    [InlineData("shred notes.txt")]
    [InlineData("find . -delete")]
    [InlineData("git clean -fd")]
    [InlineData("sudo rm -rf notes.txt")]
    [InlineData("env rm notes.txt")]
    public void RecognisedDeletesAndWrapperPrefixesAreDeleting(string command)
    {
        ShellDeleteDetector.IsDeleting(command).ShouldBeTrue();
    }

    [Fact]
    public void ScriptFileDeletionIsNotRecognised()
    {
        ShellDeleteDetector.ScriptDeletionGap.ShouldBe("script-file-deletion");
        ShellDeleteDetector.IsDeleting("bash delete-files.sh").ShouldBeFalse();
        ShellDeleteDetector.IsDeleting("./remove-notes.sh").ShouldBeFalse();
    }
}
