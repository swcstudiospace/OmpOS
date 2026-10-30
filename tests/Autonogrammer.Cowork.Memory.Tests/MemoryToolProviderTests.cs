#pragma warning disable MA0006, MA0002, IDE0005
using System.Text.Json;
using System.Text.RegularExpressions;
using Autonogrammer.Contracts.Cowork.Memory;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Cowork.Memory;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Memory.Tests;

/// <summary>Descriptor names, unavailable reasons and the agent-proposed preview.</summary>
public sealed partial class MemoryToolProviderTests
{
    /// <summary>Names match the ADR-0032 D5 rule: provider id, one underscore, no double underscore.</summary>
    [Fact]
    public void DescriptorNamesMatchTheAdr0032Rule()
    {
        var tools = Provider(Available()).Describe();
        tools.Select(static tool => tool.Name).ShouldBe(["memory_recall", "memory_save", "memory_forget"]);
        tools.ShouldAllBe(static tool => tool.ProviderId == "memory" && tool.Approval == CoworkApprovalRequirement.ByPolicy && !tool.Name.Contains("__", StringComparison.Ordinal));
        tools.Single(static tool => tool.Name == "memory_recall").Effect.ShouldBe(CoworkToolEffect.ReadOnly);
        tools.Single(static tool => tool.Name == "memory_save").Effect.ShouldBe(CoworkToolEffect.Actuating);
        tools.Single(static tool => tool.Name == "memory_forget").Effect.ShouldBe(CoworkToolEffect.Destructive);
        foreach (var tool in tools)
        {
            NameRule().IsMatch(tool.Name).ShouldBeTrue();
            tool.Name.StartsWith(tool.ProviderId + "_", StringComparison.Ordinal).ShouldBeTrue();
            tool.Name.StartsWith("cowork_", StringComparison.Ordinal).ShouldBeFalse();
        }
    }

    /// <summary>Each switch has its own unavailable reason.</summary>
    /// <param name="reason">The expected reason.</param>
    /// <param name="signedOut">Signed out.</param>
    /// <param name="offline">Offline.</param>
    /// <param name="enabled">Enabled.</param>
    /// <param name="mode">The mode.</param>
    /// <param name="paused">Paused.</param>
    /// <param name="host">Tool host reachable.</param>
    [Theory]
    [InlineData("signed_out", true, false, true, MemoryMode.On, false, true)]
    [InlineData("offline", false, true, true, MemoryMode.On, false, true)]
    [InlineData("memory_disabled", false, false, false, MemoryMode.On, false, true)]
    [InlineData("memory_off_for_task", false, false, true, MemoryMode.Off, false, true)]
    [InlineData("memory_paused", false, false, true, MemoryMode.On, true, true)]
    [InlineData("ToolHostUnavailable", false, false, true, MemoryMode.On, false, false)]
    public async Task UnavailableReasonsAreDistinct(string reason, bool signedOut, bool offline, bool enabled, MemoryMode mode, bool paused, bool host)
    {
        var facts = Available();
        facts = new MemorySessionFacts
        {
            SignedOut = signedOut,
            Offline = offline,
            Enabled = enabled,
            Mode = mode,
            Paused = paused,
            ToolHostReachable = host,
            AccountId = facts.AccountId,
        };
        var state = await Provider(facts).GetStateAsync(Session(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        state.Availability.ShouldBe(CoworkProviderAvailability.Unavailable);
        state.Reason.ShouldBe(reason);
    }

    /// <summary>The save preview carries the proposed text as agent-proposed, not as a host target.</summary>
    [Fact]
    public async Task PreviewShowsProposedTextAsAgentProposed()
    {
        var provider = Provider(Available());
        var preview = await provider.PreviewAsync(Session(), Invoke("memory_save", """{"topic":"tea","content":"oolong"}"""), TestContext.Current.CancellationToken).ConfigureAwait(true);
        preview.Target.ShouldBeNull();
        preview.Details.ShouldContain("agent-proposed: tea: oolong");
        var state = await provider.GetStateAsync(Session(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        state.Availability.ShouldBe(CoworkProviderAvailability.Available);
    }

    private static MemoryToolProvider Provider(MemorySessionFacts facts) => new(new InMemoryMemoryStore(), _ => facts);

    private static MemorySessionFacts Available() => new()
    {
        Enabled = true,
        Mode = MemoryMode.On,
        AccountId = "acct",
        ToolHostReachable = true,
    };

    private static CoworkSessionContext Session() => new()
    {
        SessionId = "session-1",
        WorkingDirectory = "/tmp",
        TrustedFolders = ["/tmp"],
        ModelReadsImages = false,
    };

    private static CoworkToolInvocation Invoke(string name, string json) =>
        new("session-1", "call-1", name, JsonDocument.Parse(json).RootElement, CoworkTurnOrigin.Local);

    [GeneratedRegex("^[a-z][a-z0-9]*(_[a-z0-9]+)*$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking)]
    private static partial Regex NameRule();
}
