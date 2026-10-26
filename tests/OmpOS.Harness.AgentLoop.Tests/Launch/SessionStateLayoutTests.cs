using OmpOS.Harness.AgentLoop.Launch;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Launch;

/// <summary>
/// Verifies the path math of the per-session state layout, the session identifier rule and the planner's file-name slug.
/// </summary>
public sealed class SessionStateLayoutTests
{
    private const string Dir = "/state/sessions/s1";

    [Fact]
    public void SessionDirectoryIsStateRootSessionsId()
    {
        SessionStateLayout.SessionDirectory("/state", "s1").ShouldBe(Dir);
    }

    [Fact]
    public void EveryPathLivesUnderTheSessionDirectoryAtItsDocumentedLocation()
    {
        SessionStateLayout.UltrathinkDirectory(Dir).ShouldBe("/state/sessions/s1/ultrathink");
        SessionStateLayout.SwarmDirectory(Dir).ShouldBe("/state/sessions/s1/swarm");
        SessionStateLayout.SwarmLiveFile(Dir).ShouldBe("/state/sessions/s1/swarm/.swarm/live.json");
        SessionStateLayout.SwarmEventsFile(Dir).ShouldBe("/state/sessions/s1/swarm/.swarm/events.jsonl");
        SessionStateLayout.SwarmGsdStateFile(Dir).ShouldBe("/state/sessions/s1/swarm/gsd-state.md");
        SessionStateLayout.ClaudeStateDirectory(Dir).ShouldBe("/state/sessions/s1/claude-state");
        SessionStateLayout.SubstratePendingDirectory(Dir).ShouldBe("/state/sessions/s1/substrate-pending");
        SessionStateLayout.ConfigOverlayFile(Dir).ShouldBe("/state/sessions/s1/omp-overlay.yml");
        SessionStateLayout.SteerDirectory(Dir).ShouldBe("/state/sessions/s1/steer");
        SessionStateLayout.ConnectionFile(Dir).ShouldBe("/state/sessions/s1/connection/report.json");
    }

    [Fact]
    public void TwoSessionsNeverShareADirectory()
    {
        SessionStateLayout.SessionDirectory("/state", "a").ShouldNotBe(SessionStateLayout.SessionDirectory("/state", "b"), StringComparer.Ordinal);
        SessionStateLayout.SwarmLiveFile(SessionStateLayout.SessionDirectory("/state", "a"))
            .ShouldStartWith("/state/sessions/a/");
    }

    [Theory]
    [InlineData("s1")]
    [InlineData("Session_A.1-b")]
    [InlineData("...")]
    [InlineData(".hidden")]
    public void ValidSessionIdsAreAccepted(string sessionId)
    {
        SessionStateLayout.IsValidSessionId(sessionId).ShouldBeTrue();
        SessionStateLayout.SessionDirectory("/state", sessionId).ShouldBe("/state/sessions/" + sessionId);
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("a/b")]
    [InlineData("../x")]
    [InlineData("a\\b")]
    [InlineData("a b")]
    [InlineData("é")]
    [InlineData("")]
    [InlineData("   ")]
    public void UnsafeSessionIdsAreRejected(string sessionId)
    {
        Should.Throw<ArgumentException>(() => SessionStateLayout.SessionDirectory("/state", sessionId));
    }

    [Fact]
    public void SessionIdLongerThan120CharactersIsRejectedAndExactly120IsAccepted()
    {
        SessionStateLayout.SessionDirectory("/state", new string('a', 120)).ShouldEndWith(new string('a', 120));
        Should.Throw<ArgumentException>(() => SessionStateLayout.SessionDirectory("/state", new string('a', 121)));
    }

    [Fact]
    public void BlankArgumentsAreRejectedEverywhere()
    {
        Should.Throw<ArgumentException>(() => SessionStateLayout.SessionDirectory(" ", "s1"));
        Should.Throw<ArgumentException>(() => SessionStateLayout.UltrathinkDirectory(""));
        Should.Throw<ArgumentException>(() => SessionStateLayout.SwarmLiveFile(" "));
        Should.Throw<ArgumentException>(() => SessionStateLayout.UltrathinkSessionFile(Dir, " "));
        Should.Throw<ArgumentException>(() => SessionStateLayout.UltrathinkSessionFile("", "abc"));
        Should.Throw<ArgumentException>(() => SessionStateLayout.SteerDirectory(" "));
        Should.Throw<ArgumentException>(() => SessionStateLayout.ConnectionFile(""));
    }

    [Theory]
    [InlineData("0198f4b2-7c1a-7d2e-9f3a-5b6c7d8e9f01", "0198f4b2-7c1a-7d2e-9f3a-5b6c7d8e9f01")]
    [InlineData("plain_id.v2-x", "plain_id.v2-x")]
    [InlineData("Claude-A", "claude-a~f501bea41454eba9")]
    [InlineData("a b/ü😀", "a_b___~4a5a51708148b1e3")]
    public void UltrathinkSessionFileKeepsAPlainIdAndSlugsAnythingElse(string acpSessionId, string expectedStem)
    {
        SessionStateLayout.UltrathinkSessionFile(Dir, acpSessionId)
            .ShouldBe("/state/sessions/s1/ultrathink/sessions/" + expectedStem + ".json");
    }

    [Fact]
    public void LongIdIsSlugTruncatedTo80CharactersBeforeTheHash()
    {
        var path = SessionStateLayout.UltrathinkSessionFile(Dir, new string('X', 100));

        path.ShouldBe("/state/sessions/s1/ultrathink/sessions/" + new string('x', 80) + "~b90aa07301e80d19.json");
    }

    [Fact]
    public void IdsThatDifferOnlyInCaseGetDistinctFiles()
    {
        SessionStateLayout.UltrathinkSessionFile(Dir, "Claude-A")
            .ShouldNotBe(SessionStateLayout.UltrathinkSessionFile(Dir, "claude-a"), StringComparer.Ordinal);
    }
}
