using System.Collections.Immutable;

namespace OmpOS.Harness.Registry;

/// <summary>
/// The only mutator of a registry. It holds the entries in memory and publishes every change to its export file before the
/// change becomes visible. The export is written to a temporary file in the same directory and renamed over the export, so a
/// failed write leaves the previous export intact and the in-memory state unchanged.
/// </summary>
/// <remarks>
/// Each successful mutation increments the generation by exactly one. A mutation that throws leaves the generation, the
/// entries and the export unchanged. Mutations are serialized, so concurrent calls apply one after another. Reads of
/// <see cref="Snapshot"/> never wait.
/// </remarks>
public sealed class RegistryWriter : IDisposable
{
    private readonly string _path;
    private readonly string _directory;
    private readonly IRegistryFileSystem _fileSystem;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private State _state;

    private RegistryWriter(string path, string directory, IRegistryFileSystem fileSystem, State state)
    {
        _path = path;
        _directory = directory;
        _fileSystem = fileSystem;
        _state = state;
    }

    /// <summary>
    /// The current generation and entries.
    /// </summary>
    public RegistrySnapshot Snapshot => Volatile.Read(ref _state).ToSnapshot();

    /// <summary>
    /// Opens the registry whose export is at <paramref name="path"/>, or starts an empty registry at generation 0 when no export
    /// exists.
    /// </summary>
    /// <param name="path">The export file. Its directory is created on the first mutation.</param>
    /// <param name="fileSystem">The storage used for publishing. The system file system when null.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The opened writer.</returns>
    /// <exception cref="InvalidRegistryExportException">
    /// The export exists but is malformed or fails its checksum. The writer refuses to open, so the file is not overwritten.
    /// </exception>
    public static async Task<RegistryWriter> OpenAsync(string path, IRegistryFileSystem? fileSystem, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("The path must name a file inside a directory.", nameof(path));

        var state = State.Empty;
        if (File.Exists(fullPath))
        {
            var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
            if (!RegistryExport.TryParse(bytes, out var snapshot, out var rejection))
            {
                throw new InvalidRegistryExportException(fullPath, rejection);
            }

            state = State.From(snapshot);
        }

        return new RegistryWriter(fullPath, directory, fileSystem ?? SystemRegistryFileSystem.Instance, state);
    }

    /// <summary>
    /// Adds an enabled entry.
    /// </summary>
    /// <param name="id">The new entry's identifier.</param>
    /// <param name="name">The display name.</param>
    /// <param name="cancellationToken">Cancels the mutation before it is applied.</param>
    /// <exception cref="RegistryEntryExistsException">An entry with this identifier already exists.</exception>
    public async Task AddAsync(string id, string name, CancellationToken cancellationToken)
    {
        ValidateId(id);
        ArgumentNullException.ThrowIfNull(name);

        await ChangeAsync(
            entries => entries.ContainsKey(id)
                ? throw new RegistryEntryExistsException("An entry with this identifier is already registered.", id)
                : entries.Add(id, new RegistryEntry(id, name, Enabled: true)),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Changes the display name of an entry, leaving its enablement unchanged.
    /// </summary>
    /// <param name="id">The entry's identifier.</param>
    /// <param name="name">The new display name.</param>
    /// <param name="cancellationToken">Cancels the mutation before it is applied.</param>
    /// <exception cref="RegistryEntryNotFoundException">No entry has this identifier.</exception>
    public async Task UpdateAsync(string id, string name, CancellationToken cancellationToken)
    {
        ValidateId(id);
        ArgumentNullException.ThrowIfNull(name);

        await ChangeAsync(entries => ReplaceEntry(entries, id, existing => existing with { Name = name }), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Enables an entry.
    /// </summary>
    /// <param name="id">The entry's identifier.</param>
    /// <param name="cancellationToken">Cancels the mutation before it is applied.</param>
    /// <exception cref="RegistryEntryNotFoundException">No entry has this identifier.</exception>
    public async Task EnableAsync(string id, CancellationToken cancellationToken)
    {
        ValidateId(id);
        await ChangeAsync(entries => ReplaceEntry(entries, id, existing => existing with { Enabled = true }), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Disables an entry.
    /// </summary>
    /// <param name="id">The entry's identifier.</param>
    /// <param name="cancellationToken">Cancels the mutation before it is applied.</param>
    /// <exception cref="RegistryEntryNotFoundException">No entry has this identifier.</exception>
    public async Task DisableAsync(string id, CancellationToken cancellationToken)
    {
        ValidateId(id);
        await ChangeAsync(entries => ReplaceEntry(entries, id, existing => existing with { Enabled = false }), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Removes an entry.
    /// </summary>
    /// <param name="id">The entry's identifier.</param>
    /// <param name="cancellationToken">Cancels the mutation before it is applied.</param>
    /// <exception cref="RegistryEntryNotFoundException">No entry has this identifier.</exception>
    public async Task RemoveAsync(string id, CancellationToken cancellationToken)
    {
        ValidateId(id);
        await ChangeAsync(
            entries => entries.TryGetValue(id, out _) ? entries.Remove(id) : throw NotFound(id),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Releases the lock that serializes mutations. The export on disk is already current after every change.
    /// </summary>
    public void Dispose() => _gate.Dispose();

    private static void ValidateId(string id) => ArgumentException.ThrowIfNullOrWhiteSpace(id);

    private static RegistryEntryNotFoundException NotFound(string id) =>
        new("No entry with this identifier is registered.", id);

    private static ImmutableSortedDictionary<string, RegistryEntry> ReplaceEntry(
        ImmutableSortedDictionary<string, RegistryEntry> entries,
        string id,
        Func<RegistryEntry, RegistryEntry> change) =>
        entries.TryGetValue(id, out var existing)
            ? entries.SetItem(id, change(existing))
            : throw NotFound(id);

    private async Task ChangeAsync(
        Func<ImmutableSortedDictionary<string, RegistryEntry>, ImmutableSortedDictionary<string, RegistryEntry>> change,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = Volatile.Read(ref _state);
            var next = new State(current.Generation + 1, change(current.Entries));
            await PublishAsync(next, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _state, next);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task PublishAsync(State next, CancellationToken cancellationToken)
    {
        var bytes = RegistryExport.Serialize(next.ToSnapshot());
        Directory.CreateDirectory(_directory);

        var temporary = Path.Combine(_directory, $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await _fileSystem.WriteAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
            _fileSystem.Replace(temporary, _path);
        }
        finally
        {
            _fileSystem.Delete(temporary);
        }
    }

    private sealed record State(long Generation, ImmutableSortedDictionary<string, RegistryEntry> Entries)
    {
        public static State Empty { get; } =
            new(0, ImmutableSortedDictionary<string, RegistryEntry>.Empty.WithComparers(StringComparer.Ordinal));

        public static State From(RegistrySnapshot snapshot) =>
            new(snapshot.Generation, snapshot.Entries.ToImmutableSortedDictionary(entry => entry.Id, entry => entry, StringComparer.Ordinal));

        public RegistrySnapshot ToSnapshot() => new(Generation, [.. Entries.Values]);
    }
}
