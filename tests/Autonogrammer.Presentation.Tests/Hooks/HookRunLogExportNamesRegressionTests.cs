using System.Text.Json;
using Autonogrammer.Presentation.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Hooks;

/// <summary>
/// Every scope and outcome is exported under its own wire name. Each enum member has an explicit entry in the table below, so a member added
/// to the enum without a name fails here instead of being exported as a neighbour's name.
/// </summary>
public sealed class HookRunLogExportNamesRegressionTests
{
    private static readonly Dictionary<HookScopeKind, string> ScopeNames = new Dictionary<HookScopeKind, string>
    {
        [HookScopeKind.Managed] = "managed",
        [HookScopeKind.Project] = "project",
        [HookScopeKind.User] = "user",
        [HookScopeKind.ClaudeLocal] = "claudeLocal",
        [HookScopeKind.ClaudeProject] = "claudeProject",
        [HookScopeKind.ClaudeUser] = "claudeUser",
        [HookScopeKind.Plugin] = "plugin",
    };

    private static readonly Dictionary<HookRunOutcome, string> StatusNames = new Dictionary<HookRunOutcome, string>
    {
        [HookRunOutcome.Succeeded] = "succeeded",
        [HookRunOutcome.Denied] = "denied",
        [HookRunOutcome.Failed] = "failed",
        [HookRunOutcome.TimedOut] = "timedOut",
        [HookRunOutcome.Skipped] = "skipped",
        [HookRunOutcome.Faulted] = "faulted",
        [HookRunOutcome.AwaitingConsent] = "awaitingConsent",
        [HookRunOutcome.Quarantined] = "pluginQuarantined",
        [HookRunOutcome.InvalidResponse] = "invalidResponse",
    };

    /// <summary>Each scope is exported under its own name.</summary>
    [Fact]
    public void EveryScopeIsExportedUnderItsOwnName()
    {
        var scopes = Enum.GetValues<HookScopeKind>();
        var log = new HookRunLogModel(TimeZoneInfo.Utc);
        log.Replace(scopes.Select(static scope => HookItems.Run(scope.ToString()) with { Scope = scope }));

        var exported = ExportedValues(log, "scope");

        foreach (var scope in scopes)
        {
            exported[scope.ToString()].ShouldBe(ScopeNames[scope]);
        }
    }

    /// <summary>Each outcome is exported under its own status name.</summary>
    [Fact]
    public void EveryOutcomeIsExportedUnderItsOwnName()
    {
        var outcomes = Enum.GetValues<HookRunOutcome>();
        var log = new HookRunLogModel(TimeZoneInfo.Utc);
        log.Replace(outcomes.Select(static outcome => HookItems.Run(outcome.ToString(), outcome)));

        var exported = ExportedValues(log, "status");

        foreach (var outcome in outcomes)
        {
            exported[outcome.ToString()].ShouldBe(StatusNames[outcome]);
        }
    }

    private static Dictionary<string, string> ExportedValues(HookRunLogModel log, string property)
    {
        using var document = JsonDocument.Parse(log.ToJson());
        return document.RootElement.EnumerateArray().ToDictionary(
            static run => run.GetProperty("hookId").GetString() ?? string.Empty,
            run => run.GetProperty(property).GetString() ?? string.Empty,
            StringComparer.Ordinal);
    }
}
