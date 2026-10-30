using System.Text.Json;

namespace Autonogrammer.Harness.AgentLoop.Connection;

/// <summary>
/// The connection report the omp plugin writes to <c>AUTONOGRAMMER_CONNECTION_FILE</c> once omp has loaded it: the raw lists of tool
/// and command names omp knows. The report is written by another process, so reading is tolerant: unknown members are
/// ignored, a missing list is empty and a missing member is "not there". A file that is empty, is not a JSON object, is larger
/// than <see cref="MaxFileBytes"/> or has another schema version is unreadable and <see cref="TryParse"/> yields null.
/// </summary>
public sealed record PluginConnectionReport
{
    /// <summary>The largest report that is read, in bytes.</summary>
    public const int MaxFileBytes = 256 * 1024;

    /// <summary>The schema version this reader understands.</summary>
    public const int SupportedVersion = 1;

    /// <summary>Gets the time the report was written, as the plugin wrote it, or null.</summary>
    public string? At { get; init; }

    /// <summary>Gets the process identifier of the omp process that wrote the report, or null.</summary>
    public long? Pid { get; init; }

    /// <summary>Gets the mode omp runs in, or null.</summary>
    public string? Mode { get; init; }

    /// <summary>Gets the omp version the report names, or null.</summary>
    public string? OmpVersion { get; init; }

    /// <summary>Gets the names of the tools omp lists. Blank entries are dropped.</summary>
    public IReadOnlyList<string> Tools { get; init; } = [];

    /// <summary>Gets the names of the slash commands omp lists, as reported (with or without the leading <c>/</c>). Blank entries are dropped.</summary>
    public IReadOnlyList<string> Commands { get; init; } = [];

    /// <summary>Gets a value indicating whether omp could list its commands. When false, <see cref="Commands"/> proves nothing and only tools are checked.</summary>
    public bool CommandsAvailable { get; init; }

    /// <summary>Reads a report.</summary>
    /// <param name="utf8">The file content.</param>
    /// <returns>The report, or null when the content is not a readable report. It never throws.</returns>
    public static PluginConnectionReport? TryParse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty || utf8.Length > MaxFileBytes)
        {
            return null;
        }

        PluginConnectionDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(utf8, ConnectionJsonContext.Default.PluginConnectionDocument);
        }
        catch (JsonException)
        {
            return null;
        }

        if (document is not { Version: SupportedVersion })
        {
            return null;
        }

        return new PluginConnectionReport
        {
            At = document.At,
            Pid = document.Pid,
            Mode = document.Mode,
            OmpVersion = document.Omp?.Version,
            Tools = Names(document.Tools),
            Commands = Names(document.Commands),
            CommandsAvailable = document.CommandsAvailable ?? false,
        };
    }

    private static List<string> Names(List<string?>? names) =>
        names is null ? [] : [.. names.Where(static name => !string.IsNullOrWhiteSpace(name)).Select(static name => name!)];
}
