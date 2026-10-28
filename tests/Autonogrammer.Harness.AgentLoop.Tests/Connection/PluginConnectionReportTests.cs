using System.Text;
using Autonogrammer.Harness.AgentLoop.Connection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Connection;

/// <summary>
/// Verifies the tolerant reader of the connection report: what a good report yields, that unknown members and comments are
/// ignored, that missing lists are empty, and that anything that is not a version 1 report object is unreadable.
/// </summary>
public sealed class PluginConnectionReportTests
{
    private static PluginConnectionReport? Parse(string json) => PluginConnectionReport.TryParse(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void AGoodReportYieldsEveryMember()
    {
        var report = Parse("{\"v\":1,\"at\":\"2026-10-26T12:00:00.000Z\",\"pid\":4242,\"mode\":\"tui\",\"omp\":{\"version\":\"18.12.5\"},\"tools\":[\"b\",\"a\"],\"commands\":[\"swarm\",\"/gsd-core\"],\"commandsAvailable\":true}");

        var parsed = report.ShouldNotBeNull();
        parsed.At.ShouldBe("2026-10-26T12:00:00.000Z");
        parsed.Pid.ShouldBe(4242);
        parsed.Mode.ShouldBe("tui");
        parsed.OmpVersion.ShouldBe("18.12.5");
        parsed.Tools.ShouldBe(["b", "a"]);
        parsed.Commands.ShouldBe(["swarm", "/gsd-core"]);
        parsed.CommandsAvailable.ShouldBeTrue();
    }

    [Fact]
    public void UnknownMembersCommentsAndTrailingCommasAreIgnored()
    {
        var report = Parse("{\"v\":1, /* note */ \"future\":{\"x\":[1,2,3]},\"tools\":[\"a\",],\"commandsAvailable\":false,}");

        var parsed = report.ShouldNotBeNull();
        parsed.Tools.ShouldBe(["a"]);
        parsed.Commands.ShouldBeEmpty();
        parsed.CommandsAvailable.ShouldBeFalse();
    }

    [Fact]
    public void MissingListsAreEmptyAndCommandsAreNotAvailableUnlessTheReportSaysSo()
    {
        var parsed = Parse("{\"v\":1,\"commands\":[\"swarm\"]}").ShouldNotBeNull();

        parsed.Tools.ShouldBeEmpty();
        parsed.CommandsAvailable.ShouldBeFalse();
        parsed.OmpVersion.ShouldBeNull();
        parsed.Pid.ShouldBeNull();
    }

    [Fact]
    public void BlankAndNullNamesAreDropped()
    {
        var parsed = Parse("{\"v\":1,\"tools\":[\"a\",null,\"\",\"  \",\"b\"]}").ShouldNotBeNull();

        parsed.Tools.ShouldBe(["a", "b"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not json")]
    [InlineData("null")]
    [InlineData("[1,2]")]
    [InlineData("\"text\"")]
    [InlineData("{}")]
    [InlineData("{\"v\":2,\"tools\":[\"a\"]}")]
    [InlineData("{\"v\":\"1\",\"tools\":[\"a\"]}")]
    [InlineData("{\"v\":1,\"tools\":\"a\"}")]
    [InlineData("{\"v\":1,\"pid\":\"x\"}")]
    public void ContentThatIsNotAVersionOneReportIsUnreadable(string json)
    {
        Parse(json).ShouldBeNull();
    }

    [Fact]
    public void TruncatedContentIsUnreadableNotAPartialReport()
    {
        var whole = Encoding.UTF8.GetBytes("{\"v\":1,\"tools\":[\"swarm_status\",\"swarm_plan\"],\"commandsAvailable\":true}");

        PluginConnectionReport.TryParse(whole.AsSpan(0, whole.Length - 20)).ShouldBeNull();
    }

    [Fact]
    public void TheSizeCapIs256KibAndExactlyTheCapIsStillRead()
    {
        PluginConnectionReport.MaxFileBytes.ShouldBe(256 * 1024);
        var prefix = "{\"v\":1,\"pad\":\"";
        var suffix = "\"}";
        var atCap = prefix + new string('x', PluginConnectionReport.MaxFileBytes - prefix.Length - suffix.Length) + suffix;
        Encoding.UTF8.GetByteCount(atCap).ShouldBe(PluginConnectionReport.MaxFileBytes);

        Parse(atCap).ShouldNotBeNull();
        Parse(atCap + " ").ShouldBeNull();
    }
}
