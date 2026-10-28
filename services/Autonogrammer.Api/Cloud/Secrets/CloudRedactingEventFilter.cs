using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Serialization;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// Cleans the events of one run before they reach the session log, the stream and any client. Every text field of every event kind passes through
/// <see cref="CloudSecretRedactor"/>. Streamed text is held back by the length of the longest secret minus one character between chunks, so a
/// secret split across two chunks is still found; the held text is released before any other event and at the end of the turn, so ordering holds.
/// An event kind this filter does not know is withheld, not passed through. A filter is used by one run and is not thread-safe.
/// </summary>
internal sealed class CloudRedactingEventFilter
{
    private readonly CloudSecretRedactor _redactor;
    private readonly StringBuilder _textCarry = new();
    private readonly StringBuilder _thoughtCarry = new();

    /// <summary>Initializes a new instance of the <see cref="CloudRedactingEventFilter"/> class.</summary>
    /// <param name="redactor">The redactor that holds the secrets of the run.</param>
    public CloudRedactingEventFilter(CloudSecretRedactor redactor)
    {
        ArgumentNullException.ThrowIfNull(redactor);
        _redactor = redactor;
    }

    /// <summary>Cleans one event and returns what may be forwarded now, in order.</summary>
    /// <param name="harnessEvent">The event the engine produced.</param>
    /// <returns>Zero or more events to forward: held text released before a different event, then the cleaned event.</returns>
    public IReadOnlyList<HarnessEvent> Process(HarnessEvent harnessEvent)
    {
        ArgumentNullException.ThrowIfNull(harnessEvent);
        var output = new List<HarnessEvent>();
        switch (harnessEvent)
        {
            case TextDeltaEvent text:
                Stream(_textCarry, text.Text, static value => new TextDeltaEvent(value), output);
                return output;
            case ThoughtEvent thought:
                Stream(_thoughtCarry, thought.Text, static value => new ThoughtEvent(value), output);
                return output;
        }

        Flush(output);
        output.Add(Clean(harnessEvent));
        return output;
    }

    /// <summary>Releases text that was held back. Call it when the stream ends.</summary>
    /// <returns>The held text as events, or none.</returns>
    public IReadOnlyList<HarnessEvent> Flush()
    {
        var output = new List<HarnessEvent>();
        Flush(output);
        return output;
    }

    private void Stream(StringBuilder carry, string incoming, Func<string, HarnessEvent> create, List<HarnessEvent> output)
    {
        if (!_redactor.HasSecrets)
        {
            output.Add(create(incoming));
            return;
        }

        var combined = _redactor.Redact(carry.ToString() + incoming) ?? string.Empty;
        var hold = Math.Min(combined.Length, _redactor.MaximumNeedleLength - 1);
        var release = combined[..^hold];
        carry.Clear();
        _ = carry.Append(combined, combined.Length - hold, hold);
        if (release.Length > 0)
        {
            output.Add(create(release));
        }
    }

    private void Flush(List<HarnessEvent> output)
    {
        if (_textCarry.Length > 0)
        {
            output.Add(new TextDeltaEvent(_redactor.Redact(_textCarry.ToString()) ?? string.Empty));
            _ = _textCarry.Clear();
        }

        if (_thoughtCarry.Length > 0)
        {
            output.Add(new ThoughtEvent(_redactor.Redact(_thoughtCarry.ToString()) ?? string.Empty));
            _ = _thoughtCarry.Clear();
        }
    }

    private HarnessEvent Clean(HarnessEvent harnessEvent) => harnessEvent switch
    {
        CompletedEvent completed => completed with { Reason = _redactor.Redact(completed.Reason) ?? string.Empty },
        ErrorEvent error => error with { Message = _redactor.Redact(error.Message) ?? string.Empty },
        DiagnosticEvent diagnostic => diagnostic with { Message = _redactor.Redact(diagnostic.Message) ?? string.Empty },
        PlanEvent plan => plan with { Markdown = _redactor.Redact(plan.Markdown) ?? string.Empty },
        ToolCallEvent call => call with { Name = _redactor.Redact(call.Name) ?? string.Empty, PayloadJson = _redactor.Redact(call.PayloadJson) ?? string.Empty },
        ToolCallUpdateEvent update => update with { Status = _redactor.Redact(update.Status) ?? string.Empty, PayloadJson = _redactor.Redact(update.PayloadJson) ?? string.Empty },
        PermissionRequestEvent request => request with { Name = _redactor.Redact(request.Name) ?? string.Empty, PayloadJson = _redactor.Redact(request.PayloadJson) ?? string.Empty },
        UiBlockEvent block => CleanBlock(block),
        _ => new DiagnosticEvent("An engine event of an unknown kind was withheld."),
    };

    private HarnessEvent CleanBlock(UiBlockEvent block)
    {
        if (!_redactor.HasSecrets)
        {
            return block;
        }

        var json = JsonSerializer.Serialize(block.Block, AutonogrammerJsonContext.Default.UiBlock);
        var cleaned = _redactor.Redact(json) ?? json;
        try
        {
            var parsed = JsonSerializer.Deserialize(cleaned, AutonogrammerJsonContext.Default.UiBlock);
            return parsed is null ? new DiagnosticEvent("A block was withheld because it could not be cleaned.") : new UiBlockEvent(parsed);
        }
        catch (JsonException)
        {
            return new DiagnosticEvent("A block was withheld because it could not be cleaned.");
        }
    }
}
