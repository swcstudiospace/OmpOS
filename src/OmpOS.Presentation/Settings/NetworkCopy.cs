using System.Globalization;

namespace OmpOS.Presentation.Settings;

/// <summary>
/// Every text of the Network page and of its diagnostics, in one place so the view carries no literal. No sentence holds an address or a
/// credential: an endpoint is shown as host or host:port only.
/// </summary>
public static class NetworkCopy
{
    /// <summary>The navigation label of the Network page.</summary>
    public const string Title = "Network";

    /// <summary>The heading of the Network page.</summary>
    public const string Heading = "Network";

    /// <summary>The sentence under the heading.</summary>
    public const string Description = "Checks whether the model provider endpoints OmpOS knows about answer from this machine.";

    /// <summary>The explanation of what a diagnostics run sends. It is static text, shown before the first run.</summary>
    public const string Explanation =
        "Run diagnostics sends one unauthenticated request to each model provider endpoint OmpOS knows about, and nothing else: no key, no token, no prompt. "
        + "An answer with any HTTP status, even an error, means the endpoint is reachable. Nothing is checked until you press the button.";

    /// <summary>The note about proxies: what the diagnostics use and what this page does not change.</summary>
    public const string ProxyNote =
        "A diagnostics run uses the system proxy settings; on Linux and macOS that means the HTTP_PROXY, HTTPS_PROXY, ALL_PROXY and NO_PROXY environment variables of the process OmpOS was started from. "
        + "The agent engine (omp) makes the model calls itself and keeps its own network configuration; this page does not change it.";

    /// <summary>The label of the button that starts a run.</summary>
    public const string RunLabel = "Run diagnostics";

    /// <summary>The label of the button that stops a run that is in progress.</summary>
    public const string CancelLabel = "Cancel";

    /// <summary>The accessible name of the polite live region that announces the result.</summary>
    public const string AnnouncementName = "Diagnostics result";

    /// <summary>The announcement while a run is in progress.</summary>
    public const string Running = "Running diagnostics.";

    /// <summary>The announcement after a run was cancelled.</summary>
    public const string Cancelled = "Diagnostics cancelled. No result was kept.";

    /// <summary>The announcement after a run failed as a whole.</summary>
    public const string RunFailed = "Diagnostics could not be run.";

    /// <summary>The text shown before the first run.</summary>
    public const string NotRun = "Diagnostics have not been run in this session.";

    /// <summary>The text shown when a run found no endpoint to check.</summary>
    public const string NoEndpoints = "There were no endpoints to check.";

    /// <summary>The word for the status of an endpoint that answered.</summary>
    public const string StatusReachable = "Reachable";

    /// <summary>The word for the status of an endpoint that could not be reached.</summary>
    public const string StatusUnreachable = "Unreachable";

    /// <summary>The word for the status of an endpoint that did not answer in time.</summary>
    public const string StatusTimedOut = "Timed out";

    /// <summary>The word for the status of an endpoint whose secure connection failed.</summary>
    public const string StatusTlsFailed = "TLS failed";

    /// <summary>The word for the status of an endpoint that was not requested.</summary>
    public const string StatusNotChecked = "Not checked";

    /// <summary>The text shown for an endpoint that cannot be shown safely.</summary>
    public const string HostHidden = "Address not shown";

    /// <summary>Gets the word for a status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The word.</returns>
    public static string StatusLabel(EndpointStatus status) => status switch
    {
        EndpointStatus.Reachable => StatusReachable,
        EndpointStatus.Unreachable => StatusUnreachable,
        EndpointStatus.TimedOut => StatusTimedOut,
        EndpointStatus.TlsFailed => StatusTlsFailed,
        _ => StatusNotChecked,
    };

    /// <summary>Builds the sentence that says what one request found out.</summary>
    /// <param name="status">The status.</param>
    /// <param name="httpStatus">The HTTP status the endpoint answered with, or null.</param>
    /// <returns>The sentence. It holds no address.</returns>
    public static string CheckMessage(EndpointStatus status, int? httpStatus) => status switch
    {
        EndpointStatus.Reachable when httpStatus is { } code => string.Create(CultureInfo.InvariantCulture, $"The endpoint answered with HTTP {code}."),
        EndpointStatus.Reachable => "The endpoint answered.",
        EndpointStatus.Unreachable => "The name did not resolve, or the connection was refused or lost.",
        EndpointStatus.TimedOut => "The endpoint did not answer in time.",
        EndpointStatus.TlsFailed => "The endpoint answered, but the secure connection could not be set up.",
        _ => "The endpoint was not checked.",
    };

    /// <summary>Builds the text of a latency.</summary>
    /// <param name="latency">The time the request took.</param>
    /// <returns>Whole milliseconds followed by the unit.</returns>
    public static string LatencyText(TimeSpan latency) =>
        string.Create(CultureInfo.InvariantCulture, $"{(long)Math.Round(latency.TotalMilliseconds)} ms");

    /// <summary>Builds the detail line of a check: what happened and how long it took.</summary>
    /// <param name="check">The check.</param>
    /// <returns>The message, followed by the latency when the request was sent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="check"/> is null.</exception>
    public static string Detail(EndpointCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);
        return check.Latency is { } latency
            ? string.Create(CultureInfo.InvariantCulture, $"{check.Message} {LatencyText(latency)}.")
            : check.Message;
    }

    /// <summary>Builds the sentence a screen reader speaks for one result line.</summary>
    /// <param name="name">The provider.</param>
    /// <param name="host">The endpoint as shown.</param>
    /// <param name="statusLabel">The word for the status.</param>
    /// <param name="detail">The detail line.</param>
    /// <returns>The sentence.</returns>
    public static string RowSummary(string name, string host, string statusLabel, string detail)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(statusLabel);
        ArgumentNullException.ThrowIfNull(detail);
        return string.Create(CultureInfo.InvariantCulture, $"{name}, {host}: {statusLabel}. {detail}");
    }

    /// <summary>Builds the announcement of a finished run.</summary>
    /// <param name="reachable">How many endpoints answered.</param>
    /// <param name="total">How many endpoints were listed.</param>
    /// <returns>The sentence.</returns>
    public static string Finished(int reachable, int total) =>
        string.Create(CultureInfo.InvariantCulture, $"Diagnostics finished. {reachable} of {total} endpoints reachable.");

    /// <summary>Builds the line that says when the result was made.</summary>
    /// <param name="at">When the run finished.</param>
    /// <returns>The sentence, in UTC.</returns>
    public static string CheckedAt(DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"Checked {at.UtcDateTime:yyyy-MM-dd HH:mm:ss} UTC.");
}
