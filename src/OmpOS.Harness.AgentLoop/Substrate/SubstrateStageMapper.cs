using System.Globalization;
using OmpOS.Contracts.Agents;

namespace OmpOS.Harness.AgentLoop.Substrate;

/// <summary>
/// Maps Substrate answers to <see cref="StageEvent"/>s. Every text that reaches an event is built from a closed set of fields and
/// cleaned with <see cref="SubstrateText.Sanitize"/>. Ledger events use only their time, kind, actor and the declared payload
/// action, item and outcome, so the summary and any other payload text can never reach a panel, and lease events carry the
/// holder surface and the expiry, never a lease or session identifier.
/// </summary>
internal static class SubstrateStageMapper
{
    /// <summary>The stage identifier of the health stage.</summary>
    internal const string HealthStageId = "health";

    /// <summary>The stage identifier of the brief stage.</summary>
    internal const string BriefStageId = "brief";

    /// <summary>The stage identifier of the lease stage.</summary>
    internal const string LeaseStageId = "lease";

    /// <summary>The stage identifier of the ledger stage, the parent of the event stages.</summary>
    internal const string LedgerStageId = "ledger";

    /// <summary>The prefix of an event stage identifier.</summary>
    internal const string EventStagePrefix = "event:";

    private const int FieldCap = 40;
    private const int MaxLeaseEntries = 3;

    /// <summary>Builds the health stage for a reachable server.</summary>
    /// <param name="context">The session and clock facts.</param>
    /// <param name="health">What the server reported.</param>
    /// <returns>A Succeeded event.</returns>
    internal static StageEvent Health(MappingContext context, SubstrateHealth health)
    {
        ArgumentNullException.ThrowIfNull(health);
        var detail = string.Create(
            CultureInfo.InvariantCulture,
            $"Substrate is reachable; index {Flag(health.IndexReachable, "up", "down")}, ledger {Flag(health.LedgerReachable, "up", "down")}, events {Flag(health.EventsWritable, "writable", "read-only")}.");
        return Create(context, HealthStageId, "Substrate", StageStatus.Succeeded, detail, null);
    }

    /// <summary>Builds the health stage for an unreachable or unhealthy server.</summary>
    /// <param name="context">The session and clock facts.</param>
    /// <param name="failure">Why the health check failed.</param>
    /// <returns>An Unavailable event.</returns>
    internal static StageEvent HealthUnavailable(MappingContext context, SubstrateUnavailableException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return Create(context, HealthStageId, "Substrate", StageStatus.Unavailable, Describe("Substrate is unavailable", failure) + " Retrying with backoff.", null);
    }

    /// <summary>Builds the brief stage.</summary>
    /// <param name="context">The session and clock facts.</param>
    /// <param name="brief">The brief the server composed.</param>
    /// <returns>A Succeeded event with section and item counts, or a Degraded event naming the failed sources.</returns>
    internal static StageEvent Brief(MappingContext context, SubstrateBrief brief)
    {
        ArgumentNullException.ThrowIfNull(brief);
        var sections = 0;
        var items = 0;
        foreach (var raw in brief.Text.Split('\n'))
        {
            var line = raw.TrimStart();
            if (line.StartsWith('#'))
            {
                sections++;
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                items++;
            }
        }

        var complete = string.Equals(brief.State, "complete", StringComparison.Ordinal) && brief.FailedSources.Count == 0;
        if (complete)
        {
            return Create(context, BriefStageId, "Substrate brief", StageStatus.Succeeded, string.Create(CultureInfo.InvariantCulture, $"{sections} sections, {items} items."), null);
        }

        var failed = brief.FailedSources.Count == 0 ? string.Empty : " Unavailable sources: " + string.Join(", ", brief.FailedSources) + ".";
        return Create(context, BriefStageId, "Substrate brief", StageStatus.Degraded, string.Create(CultureInfo.InvariantCulture, $"The brief is {brief.State}: {sections} sections, {items} items.{failed}"), null);
    }

    /// <summary>Builds a Degraded stage for a part that could not be read.</summary>
    /// <param name="context">The session and clock facts.</param>
    /// <param name="stageId">The stage that failed.</param>
    /// <param name="title">The stage title.</param>
    /// <param name="what">What could not be read.</param>
    /// <param name="failure">The typed failure, or null when the cause was a malformed reply.</param>
    /// <param name="parentStageId">The parent stage, or null.</param>
    /// <returns>A Degraded event.</returns>
    internal static StageEvent PartDegraded(MappingContext context, string stageId, string title, string what, SubstrateUnavailableException? failure, string? parentStageId)
    {
        var detail = failure is null ? what + " (unreadable reply)." : Describe(what, failure);
        return Create(context, stageId, title, StageStatus.Degraded, detail, parentStageId);
    }

    /// <summary>Builds the lease stage for a graph the server does not know.</summary>
    /// <param name="context">The session and clock facts.</param>
    /// <returns>A Degraded event.</returns>
    internal static StageEvent LeaseNotRegistered(MappingContext context) =>
        Create(context, LeaseStageId, "Task lease", StageStatus.Degraded, "The graph is not registered in the Substrate index.", null);

    /// <summary>Builds the lease stage from a graph. Only the holder surface and the expiry are shown.</summary>
    /// <param name="context">The session and clock facts.</param>
    /// <param name="graph">The graph as <c>graph_get</c> returned it.</param>
    /// <returns>Running while a lease is held, Degraded when only expired leases remain, Pending when nothing is held.</returns>
    internal static StageEvent Lease(MappingContext context, SubstrateWire.GraphViewDto graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var held = new List<string>();
        var expired = new List<string>();
        foreach (var node in graph.Nodes ?? [])
        {
            var surface = SubstrateText.Sanitize(node?.ClaimedBy, FieldCap);
            if (node is null || surface.Length == 0)
            {
                continue;
            }

            var expiry = ParseTime(node.LeaseExpiresAt);
            if (expiry is null)
            {
                held.Add(surface + " until unknown");
            }
            else if (expiry > context.Now)
            {
                held.Add(surface + " until " + FormatTime(expiry.Value));
            }
            else
            {
                expired.Add(surface + " expired " + FormatTime(expiry.Value));
            }
        }

        if (held.Count > 0)
        {
            return Create(context, LeaseStageId, "Task lease", StageStatus.Running, "Held by " + Join(held) + ".", null);
        }

        if (expired.Count > 0)
        {
            return Create(context, LeaseStageId, "Task lease", StageStatus.Degraded, "Lease lapsed: " + Join(expired) + ".", null);
        }

        return Create(context, LeaseStageId, "Task lease", StageStatus.Pending, "No node lease is held.", null);
    }

    /// <summary>Builds the ledger parent stage.</summary>
    /// <param name="context">The session and clock facts.</param>
    /// <param name="page">The events the ledger returned.</param>
    /// <returns>A Succeeded event with the event count.</returns>
    internal static StageEvent Ledger(MappingContext context, SubstrateEventPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var more = page.Truncated ? " More exist." : string.Empty;
        return Create(context, LedgerStageId, "Event ledger", StageStatus.Succeeded, string.Create(CultureInfo.InvariantCulture, $"{page.Events.Count} recent events.{more}"), null);
    }

    /// <summary>Builds the stage of one ledger event from its time, kind, actor, action, item and outcome only.</summary>
    /// <param name="context">The session and clock facts.</param>
    /// <param name="ledgerEvent">The event, already reduced to the fields a panel may show.</param>
    /// <returns>The event stage, or null when the event identifier is empty after cleaning.</returns>
    internal static StageEvent? Event(MappingContext context, SubstrateLedgerEvent ledgerEvent)
    {
        ArgumentNullException.ThrowIfNull(ledgerEvent);
        var id = StageIdPart(ledgerEvent.Id);
        if (id.Length == 0)
        {
            return null;
        }

        var kind = SubstrateText.Sanitize(ledgerEvent.Kind, FieldCap);
        var action = SubstrateText.Sanitize(ledgerEvent.Action, FieldCap);
        var title = kind.Length == 0 ? "event" : kind;
        if (action.Length > 0)
        {
            title += " " + action;
        }

        var parts = new List<string>();
        if (ledgerEvent.Timestamp is { } at)
        {
            parts.Add(FormatTime(at));
        }

        AddPart(parts, "actor", ledgerEvent.Actor);
        AddPart(parts, "item", ledgerEvent.Item);
        AddPart(parts, "outcome", ledgerEvent.Outcome);
        var status = string.Equals(kind, "warning", StringComparison.Ordinal) ? StageStatus.Degraded : StageStatus.Succeeded;
        return new StageEvent
        {
            SessionId = context.SessionId,
            Source = StageSource.Substrate,
            StageId = EventStagePrefix + id,
            Title = title,
            Status = status,
            Timestamp = ledgerEvent.Timestamp ?? context.Now,
            Detail = parts.Count == 0 ? null : string.Join(", ", parts),
            ParentStageId = LedgerStageId,
            GraphId = context.GraphId,
        };
    }

    private static void AddPart(List<string> parts, string label, string? value)
    {
        var clean = SubstrateText.Sanitize(value, FieldCap);
        if (clean.Length > 0)
        {
            parts.Add(label + " " + clean);
        }
    }

    private static string StageIdPart(string id)
    {
        var chars = id.Where(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or ':').Take(64).ToArray();
        return new string(chars);
    }

    private static string Join(List<string> entries)
    {
        var shown = string.Join("; ", entries.Take(MaxLeaseEntries));
        return entries.Count > MaxLeaseEntries ? string.Create(CultureInfo.InvariantCulture, $"{shown} (+{entries.Count - MaxLeaseEntries} more)") : shown;
    }

    private static string Describe(string what, SubstrateUnavailableException failure)
    {
        var status = failure.StatusCode is { } code ? string.Create(CultureInfo.InvariantCulture, $", HTTP {code}") : string.Empty;
        return string.Create(CultureInfo.InvariantCulture, $"{what} ({failure.Reason}{status}).");
    }

    private static string Flag(bool? value, string yes, string no) => value is null ? "unknown" : value.Value ? yes : no;

    private static DateTimeOffset? ParseTime(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value) ? value : null;

    private static string FormatTime(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static StageEvent Create(MappingContext context, string stageId, string title, StageStatus status, string detail, string? parentStageId) =>
        new()
        {
            SessionId = context.SessionId,
            Source = StageSource.Substrate,
            StageId = stageId,
            Title = title,
            Status = status,
            Timestamp = context.Now,
            Detail = detail,
            ParentStageId = parentStageId,
            GraphId = context.GraphId,
        };

    /// <summary>The session and clock facts every mapped event shares.</summary>
    /// <param name="SessionId">The session the events belong to.</param>
    /// <param name="GraphId">The Graph identifier of the session, or null.</param>
    /// <param name="Now">The observation time.</param>
    internal sealed record MappingContext(string SessionId, string? GraphId, DateTimeOffset Now);
}
