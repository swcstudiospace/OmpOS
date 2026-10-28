namespace Autonogrammer.Harness.Secrets.Tests;

/// <summary>
/// Holds registered canary secrets and reports every occurrence of one in the output of a sink. A non-empty report is a
/// failure: a secret reached a log, transcript, crash report or telemetry channel.
/// </summary>
internal sealed class CanaryHarness
{
    private readonly List<string> _canaries = [];

    /// <summary>Registers a secret whose value must never appear in any sink.</summary>
    /// <param name="canary">The secret to watch for.</param>
    public void Register(SecretString canary)
    {
        ArgumentNullException.ThrowIfNull(canary);
        _canaries.Add(canary.Reveal());
    }

    /// <summary>Scans the combined output of every sink for a registered canary.</summary>
    /// <param name="sinks">The sinks to scan.</param>
    /// <returns>
    /// One entry per canary occurrence, in sink, canary and offset order. Empty when the scan is clean. Each channel is
    /// scanned as the concatenation of its writes, so a canary split across several writes is still found.
    /// </returns>
    public IReadOnlyList<CanaryLeak> Inspect(IEnumerable<InMemorySecretSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        List<CanaryLeak> leaks = [];
        foreach (InMemorySecretSink sink in sinks)
        {
            string output = string.Concat(sink.Writes);
            for (int canaryIndex = 0; canaryIndex < _canaries.Count; canaryIndex++)
            {
                int offset = output.IndexOf(_canaries[canaryIndex], StringComparison.Ordinal);
                while (offset >= 0)
                {
                    leaks.Add(new CanaryLeak(sink.Channel, canaryIndex, offset));
                    offset = output.IndexOf(_canaries[canaryIndex], offset + 1, StringComparison.Ordinal);
                }
            }
        }

        return leaks;
    }
}
