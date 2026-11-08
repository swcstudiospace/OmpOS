using System.Text.RegularExpressions;
using Autonogrammer.Contracts.Environments;

namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// Reads the probe's answer. Every fact must be present exactly once, have a known name, and have the shape that fact has. A fact that does not
/// fit is a failure of the whole answer, never a value taken from a guess. Lines without the probe prefix are ignored, so a banner on stdout
/// does not spoil an otherwise complete answer.
/// </summary>
internal static partial class RemoteProbeParser
{
    private const string Absent = "ABSENT";
    private const int MaxPathLength = 4096;

    private static readonly HashSet<string> KnownKeys = new(StringComparer.Ordinal)
    {
        "system", "machine", "home",
        "tool_bash", "tool_curl", "tool_tar", "tool_unzip", "tool_git", "tool_sha256", "tool_sha512",
        "omp_path", "omp_version", "bun_version", "bundle",
    };

    /// <summary>Reads one probe answer.</summary>
    /// <param name="output">The probe's stdout.</param>
    /// <param name="observedAt">The time the probe was read, stored in the state.</param>
    /// <returns>The state, or the reason the answer does not parse.</returns>
    public static RemoteProbeOutcome Parse(string output, DateTimeOffset observedAt)
    {
        ArgumentNullException.ThrowIfNull(output);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (!line.StartsWith(RemoteProbe.Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (ReadFact(line, values) is { } failure)
            {
                return RemoteProbeOutcome.Unreadable(failure);
            }
        }

        if (KnownKeys.Where(key => !values.ContainsKey(key)).Order(StringComparer.Ordinal).FirstOrDefault() is { } missing)
        {
            return RemoteProbeOutcome.Unreadable("the fact " + missing + " is missing");
        }

        try
        {
            return RemoteProbeOutcome.Parsed(Build(values, observedAt));
        }
        catch (InvalidDataException exception)
        {
            return RemoteProbeOutcome.Unreadable(exception.Message);
        }
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_.-]{0,31}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"^(?:[A-Za-z][A-Za-z0-9._-]*[ /])?v?(?<version>[0-9]{1,6}\.[0-9]{1,6}\.[0-9]{1,6})\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex OmpVersionPattern();

    [GeneratedRegex(@"^v?(?<version>[0-9]{1,6}\.[0-9]{1,6}\.[0-9]{1,6})\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BunVersionPattern();

    private static string? ReadFact(string line, Dictionary<string, string> values)
    {
        var pair = line[RemoteProbe.Prefix.Length..];
        var separator = pair.IndexOf('=', StringComparison.Ordinal);
        if (separator <= 0)
        {
            return "a probe line has no fact name";
        }

        var key = pair[..separator];
        if (!KnownKeys.Contains(key))
        {
            return "the probe names an unknown fact " + key;
        }

        return values.TryAdd(key, pair[(separator + 1)..]) ? null : "the probe names the fact " + key + " twice";
    }

    private static RemoteState Build(Dictionary<string, string> values, DateTimeOffset observedAt)
    {
        var kernel = Name(values, "system");
        var machine = Name(values, "machine");
        var home = values["home"];
        if (!SshProfileRules.IsValidAbsoluteRemotePath(home))
        {
            throw new InvalidDataException("home is not an absolute path");
        }

        var (ompPath, ompVersion) = ReadOmp(values);
        var (bundlePresent, plugins) = ReadPlugins(values);
        return new RemoteState(
            observedAt,
            kernel,
            machine,
            home,
            ReadTools(values),
            ompPath,
            ompVersion,
            ReadBun(values),
            bundlePresent,
            plugins);
    }

    private static string Name(Dictionary<string, string> values, string key)
    {
        var value = values[key];
        return NamePattern().IsMatch(value) ? value : throw new InvalidDataException(key + " is not a name");
    }

    private static RemoteTools ReadTools(IReadOnlyDictionary<string, string> values) => new(
        Bash: Flag(values, "tool_bash"),
        Curl: Flag(values, "tool_curl"),
        Tar: Flag(values, "tool_tar"),
        Unzip: Flag(values, "tool_unzip"),
        Git: Flag(values, "tool_git"),
        Sha256: Flag(values, "tool_sha256"),
        Sha512: Flag(values, "tool_sha512"));

    private static bool Flag(IReadOnlyDictionary<string, string> values, string key) => values[key] switch
    {
        "yes" => true,
        "no" => false,
        _ => throw new InvalidDataException("the fact " + key + " is neither yes nor no"),
    };

    private static (string? Path, string? Version) ReadOmp(Dictionary<string, string> values)
    {
        var path = values["omp_path"];
        var version = values["omp_version"];
        if (string.Equals(path, Absent, StringComparison.Ordinal))
        {
            if (!string.Equals(version, Absent, StringComparison.Ordinal))
            {
                throw new InvalidDataException("omp_version is given, but omp is absent");
            }

            return (null, null);
        }

        if (path.Length == 0 || path.Length > MaxPathLength || path.Any(char.IsControl))
        {
            throw new InvalidDataException("omp_path is not a printable path");
        }

        if (string.Equals(version, Absent, StringComparison.Ordinal))
        {
            throw new InvalidDataException("omp is on the path, but omp_version is absent");
        }

        var match = OmpVersionPattern().Match(version);
        return match.Success
            ? (path, match.Groups["version"].Value)
            : throw new InvalidDataException("omp --version printed an unrecognized line");
    }

    private static string? ReadBun(Dictionary<string, string> values)
    {
        var version = values["bun_version"];
        if (string.Equals(version, Absent, StringComparison.Ordinal))
        {
            return null;
        }

        var match = BunVersionPattern().Match(version);
        return match.Success ? match.Groups["version"].Value : throw new InvalidDataException("the Bun under ~/.bun/bin printed an unrecognized version");
    }

    private static (bool Present, IReadOnlyList<RemotePluginEntry> Plugins) ReadPlugins(Dictionary<string, string> values)
    {
        var text = values["bundle"];
        if (string.Equals(text, Absent, StringComparison.Ordinal))
        {
            return (false, Array.Empty<RemotePluginEntry>());
        }

        IReadOnlyList<PluginBundleRecord> records;
        try
        {
            records = PluginBundleRecordReader.Parse(text);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidDataException("the remote plugin bundle record is not valid: " + exception.Message, exception);
        }

        return (true, records.Select(static record => new RemotePluginEntry(record.Dir, record.Commit, record.ToJson())).ToList());
    }
}
