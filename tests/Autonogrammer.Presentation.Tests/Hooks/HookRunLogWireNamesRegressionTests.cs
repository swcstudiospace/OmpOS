using System.Text.Json;
using Autonogrammer.Presentation.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Hooks;

/// <summary>The Hooks log copies each outcome under the wire string the contract documents, so a renamed status cannot pass unnoticed.</summary>
public sealed class HookRunLogWireNamesRegressionTests
{
    /// <summary>Each outcome is copied as JSON under its documented wire string.</summary>
    /// <param name="outcome">The outcome the run recorded.</param>
    /// <param name="wire">The wire string the contract documents.</param>
    [Theory]
    [InlineData(HookRunOutcome.Succeeded, "succeeded")]
    [InlineData(HookRunOutcome.Denied, "denied")]
    [InlineData(HookRunOutcome.Failed, "failed")]
    [InlineData(HookRunOutcome.TimedOut, "timedOut")]
    [InlineData(HookRunOutcome.Skipped, "skipped")]
    [InlineData(HookRunOutcome.Faulted, "faulted")]
    [InlineData(HookRunOutcome.AwaitingConsent, "awaitingConsent")]
    [InlineData(HookRunOutcome.Quarantined, "pluginQuarantined")]
    [InlineData(HookRunOutcome.InvalidResponse, "invalidResponse")]
    public void EveryOutcomeIsCopiedUnderItsWireName(HookRunOutcome outcome, string wire)
    {
        var log = new HookRunLogModel(TimeZoneInfo.Utc);
        log.Replace([HookItems.Run("hook", outcome)]);

        using var json = JsonDocument.Parse(log.ToJson());

        json.RootElement[0].GetProperty("status").GetString().ShouldBe(wire);
    }
}
