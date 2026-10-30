using System.Globalization;
using Autonogrammer.Contracts.Hooks;

namespace Autonogrammer.Presentation.Hooks;

/// <summary>One line of the hook run log, as the page shows it. Every text field has been through <see cref="HookLogRedaction"/>.</summary>
public sealed class HookLogRow
{
    private HookLogRow(HookRunItem run, string timeText, string? userMessage, string? output, string? errorOutput)
    {
        Run = run;
        TimeText = timeText;
        UserMessage = userMessage ?? string.Empty;
        Output = output ?? string.Empty;
        ErrorOutput = errorOutput ?? string.Empty;
    }

    /// <summary>Gets the run the row describes. Its text fields are the source's, not redacted: show the row's own properties instead.</summary>
    public HookRunItem Run { get; }

    /// <summary>Gets the time of the run as hours, minutes and seconds in the zone the log was created with.</summary>
    public string TimeText { get; }

    /// <summary>Gets the event the hook ran for.</summary>
    public string EventName => Run.EventName;

    /// <summary>Gets the name of the hook.</summary>
    public string HookName => Run.HookName;

    /// <summary>Gets the heading of the scope the hook comes from.</summary>
    public string ScopeTitle => HooksCopy.ScopeTitle(Run.Scope);

    /// <summary>Gets what happened, in words.</summary>
    public string OutcomeText => HooksCopy.OutcomeText(Run.Outcome);

    /// <summary>Gets the hook's own verdict: allow, ask, deny or none.</summary>
    public string DecisionText => Run.Decision;

    /// <summary>Gets the exit code, or an empty string when the process did not exit.</summary>
    public string ExitCodeText => Run.ExitCode is { } code ? code.ToString(CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>Gets how long the hook ran, for example <c>120 ms</c>.</summary>
    public string DurationText => string.Create(CultureInfo.InvariantCulture, $"{Run.DurationMs} ms");

    /// <summary>Gets the value the matcher was tested against, or an empty string.</summary>
    public string MatchedValue => Run.MatchedValue ?? string.Empty;

    /// <summary>Gets the message the hook asked to show the user, redacted, or an empty string.</summary>
    public string UserMessage { get; }

    /// <summary>Gets the hook's standard output, redacted, followed by the cut notice when the output was cut; or an empty string.</summary>
    public string Output { get; }

    /// <summary>Gets the hook's standard error, redacted, or an empty string.</summary>
    public string ErrorOutput { get; }

    /// <summary>Gets a short explanation of the run, or an empty string.</summary>
    public string Note => Run.Note ?? string.Empty;

    /// <summary>Gets a value indicating whether the row has output or an explanation to expand.</summary>
    public bool HasDetails => Output.Length > 0 || ErrorOutput.Length > 0 || Note.Length > 0;

    /// <summary>Gets a value indicating whether the run did not go well: it failed, timed out, was denied or refused.</summary>
    public bool IsProblem => Run.Outcome != HookRunOutcome.Succeeded && Run.Outcome != HookRunOutcome.Skipped;

    /// <summary>Gets the accessible name of the row.</summary>
    public string AutomationName => string.Create(CultureInfo.InvariantCulture, $"{TimeText}, {HookName}, {EventName}: {OutcomeText}, {DurationText}");

    /// <summary>Builds the row of a run.</summary>
    /// <param name="run">The run.</param>
    /// <param name="zone">The time zone the time is shown in.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static HookLogRow From(HookRunItem run, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(zone);
        var time = TimeZoneInfo.ConvertTime(run.At, zone).ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        var output = HookLogRedaction.Redact(run.StandardOutputExcerpt);
        if (run.OutputTruncated)
        {
            output = string.IsNullOrEmpty(output) ? HooksCopy.OutputCut : output + "\n" + HooksCopy.OutputCut;
        }

        return new HookLogRow(run, time, HookLogRedaction.Redact(run.UserMessage), output, HookLogRedaction.Redact(run.StandardErrorExcerpt));
    }
}
