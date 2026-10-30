namespace Autonogrammer.Presentation.Export;

/// <summary>
/// The token usage of one run: when it happened, which model served it, and how many input and output tokens it used.
/// </summary>
public sealed record UsageRecord
{
    /// <summary>Creates a usage record. The timestamp is stored in UTC.</summary>
    /// <param name="runId">The identifier of the run. Must not be empty or white space.</param>
    /// <param name="timestamp">When the run happened. Any offset is accepted and converted to UTC.</param>
    /// <param name="model">The model that served the run. Must not be empty or white space.</param>
    /// <param name="inputTokens">The input tokens the run used. Must not be negative.</param>
    /// <param name="outputTokens">The output tokens the run used. Must not be negative.</param>
    /// <exception cref="ArgumentException"><paramref name="runId"/> or <paramref name="model"/> is null, empty or white space.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A token count is negative.</exception>
    public UsageRecord(string runId, DateTimeOffset timestamp, string model, long inputTokens, long outputTokens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentOutOfRangeException.ThrowIfNegative(inputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(outputTokens);

        RunId = runId;
        TimestampUtc = timestamp.ToUniversalTime();
        Model = model;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
    }

    /// <summary>Gets the identifier of the run.</summary>
    public string RunId { get; }

    /// <summary>Gets when the run happened, in UTC.</summary>
    public DateTimeOffset TimestampUtc { get; }

    /// <summary>Gets the model that served the run.</summary>
    public string Model { get; }

    /// <summary>Gets the input tokens the run used.</summary>
    public long InputTokens { get; }

    /// <summary>Gets the output tokens the run used.</summary>
    public long OutputTokens { get; }

    /// <summary>Gets the input and output tokens together.</summary>
    public long TotalTokens => InputTokens + OutputTokens;
}
