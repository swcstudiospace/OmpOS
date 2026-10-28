using System.Globalization;
using System.Text.Json;

namespace Autonogrammer.Sandbox;

/// <summary>
/// The transcript of one cloud session, stored as JSON lines in a file on the session's volume.
/// The volume is the only source of truth: nothing about the transcript lives in container memory.
/// </summary>
public sealed class CloudTranscriptVolume
{
    /// <summary>The default transcript file name on the volume.</summary>
    public const string DefaultPath = "transcript.jsonl";

    private readonly ICloudEnvironmentClient _client;
    private readonly string _volumeId;
    private readonly string _path;

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudTranscriptVolume"/> class.
    /// </summary>
    /// <param name="client">The cloud environment client that owns the volume.</param>
    /// <param name="volumeId">The volume holding the transcript.</param>
    /// <param name="path">The transcript file path relative to the volume root.</param>
    public CloudTranscriptVolume(ICloudEnvironmentClient client, string volumeId, string path = DefaultPath)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(volumeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _client = client;
        _volumeId = volumeId;
        _path = path;
    }

    /// <summary>
    /// Appends one entry to the transcript file.
    /// </summary>
    /// <param name="entry">The entry to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the entry is on the volume.</returns>
    public Task AppendAsync(CloudTranscriptEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var line = JsonSerializer.Serialize(entry, CloudTranscriptJsonContext.Default.CloudTranscriptEntry) + "\n";
        return _client.AppendVolumeFileAsync(_volumeId, _path, line, cancellationToken);
    }

    /// <summary>
    /// Reads every stored entry in the order it was appended.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored entries; empty when nothing was stored yet.</returns>
    /// <exception cref="InvalidDataException">A stored line is not a transcript entry.</exception>
    public async Task<IReadOnlyList<CloudTranscriptEntry>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var text = await _client.ReadVolumeFileAsync(_volumeId, _path, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var entries = new List<CloudTranscriptEntry>();
        var lineNumber = 0;
        foreach (var line in text.Split('\n'))
        {
            lineNumber++;
            if (line.Length == 0)
            {
                continue;
            }

            CloudTranscriptEntry? entry;
            try
            {
                entry = JsonSerializer.Deserialize(line, CloudTranscriptJsonContext.Default.CloudTranscriptEntry);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(
                    string.Create(CultureInfo.InvariantCulture, $"Transcript '{_path}' line {lineNumber} is not a transcript entry."),
                    ex);
            }

            entries.Add(entry ?? throw new InvalidDataException(
                string.Create(CultureInfo.InvariantCulture, $"Transcript '{_path}' line {lineNumber} is null.")));
        }

        return entries;
    }
}
