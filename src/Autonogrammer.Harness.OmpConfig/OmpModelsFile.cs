using System.Globalization;
using System.Text;

namespace Autonogrammer.Harness.OmpConfig;

/// <summary>
/// Edits the Autonogrammer-owned blocks of omp's <c>models.yml</c> (in the agent directory) and nothing else. Each managed provider is a
/// block between the comment lines <c># autonogrammer:begin &lt;id&gt;</c> and <c># autonogrammer:end &lt;id&gt;</c>, placed as a child of the root
/// <c>providers:</c> mapping; every byte outside the blocks (comments, ordering, line endings, a missing final newline) is
/// preserved because the file is spliced line by line, never re-serialised.
/// </summary>
/// <remarks>
/// A write keeps the previous file as <c>models.yml.bak-autonogrammer-&lt;UTC yyyyMMddTHHmmssZ&gt;</c> (the newest five are kept), writes a
/// temporary file in the same directory with owner-only permissions, flushes it and renames it over the target. It then asks the
/// real omp (<c>omp models --json --no-extensions</c>) to load the file: omp exits 0 even when <c>models.yml</c> is invalid and only
/// warns on standard error, so that warning is the validation. When it appears, or omp cannot be asked, an upsert restores the
/// previous bytes from the backup. The file is created with <c>providers:</c> when it does not exist. Writes in one process are
/// serialised; there is no cross-process lock.
/// </remarks>
public sealed class OmpModelsFile
{
    /// <summary>The file name omp reads in its agent directory.</summary>
    public const string FileName = "models.yml";

    /// <summary>The alternative file name omp also accepts. When it exists Autonogrammer does not write, because it cannot tell which file omp reads.</summary>
    public const string AlternativeFileName = "models.yaml";

    /// <summary>The prefix of backup file names; the UTC timestamp follows.</summary>
    public const string BackupPrefix = "models.yml.bak-autonogrammer-";

    /// <summary>How many backups are kept.</summary>
    public const int BackupsToKeep = 5;

    private const UnixFileMode OwnerReadWrite = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode OwnerOnlyDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const int MaxBackupNameAttempts = 99;

    // One writer at a time per process. Static so the field needs no disposal; the cost is that two agent directories take turns.
    private static readonly SemaphoreSlim WriteGate = new(1, 1);
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly OmpConfigOptions _options;
    private readonly OmpCommandRunner _runner;
    private readonly string _agentDirectory;

    /// <summary>Creates the editor.</summary>
    /// <param name="options">The omp location and agent directory.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public OmpModelsFile(OmpConfigOptions options)
        : this(options, new OmpCommandRunner(options))
    {
    }

    /// <summary>Creates the editor with a shared runner.</summary>
    /// <param name="options">The omp location and agent directory.</param>
    /// <param name="runner">The runner that starts omp for validation.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public OmpModelsFile(OmpConfigOptions options, OmpCommandRunner runner)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(runner);
        _options = options;
        _runner = runner;
        _agentDirectory = options.ResolveAgentDirectory();
    }

    /// <summary>Gets the absolute path of <c>models.yml</c>.</summary>
    public string FilePath => Path.Combine(_agentDirectory, FileName);

    /// <summary>
    /// Reads the Autonogrammer blocks back into providers. Nothing outside the blocks is interpreted. A missing file reads as no providers.
    /// A block that is not in the layout Autonogrammer writes is reported in <see cref="OmpManagedReadResult.Problems"/> and skipped.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The managed providers and the problems found.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<OmpManagedReadResult> ReadManagedAsync(CancellationToken cancellationToken)
    {
        string text;
        try
        {
            if (!File.Exists(FilePath))
            {
                return new OmpManagedReadResult([], []);
            }

            var bytes = await File.ReadAllBytesAsync(FilePath, cancellationToken).ConfigureAwait(false);
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return new OmpManagedReadResult([], ["models.yml is not valid UTF-8."]);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new OmpManagedReadResult([], [$"models.yml could not be read ({exception.GetType().Name})."]);
        }

        return OmpModelsEditor.ReadManaged(text);
    }

    /// <summary>
    /// Adds the block of <paramref name="provider"/>, or replaces it when Autonogrammer already wrote one with that id. The provider is
    /// validated first (nothing is touched when it is not acceptable). An id that exists outside an Autonogrammer block is a
    /// <see cref="OmpModelsWriteStatus.Conflict"/>.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="cancellationToken">Cancels the write; a cancellation after the file changed restores the previous file and throws.</param>
    /// <returns>What happened.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<OmpModelsWriteResult> UpsertAsync(OmpManagedProvider provider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var problem = OmpManagedProviderValidator.Validate(provider);
        if (problem is not null)
        {
            return new OmpModelsWriteResult(OmpModelsWriteStatus.Rejected, problem);
        }

        return await ApplyAsync(text => OmpModelsEditor.Upsert(text, provider), rollbackOnRejection: true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes the block of provider <paramref name="id"/>. A missing block is <see cref="OmpModelsWriteStatus.Unchanged"/>. omp is
    /// asked to load the result, but a removal is never rolled back: removing is how a broken block is repaired.
    /// </summary>
    /// <param name="id">The provider id.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>What happened.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<OmpModelsWriteResult> RemoveAsync(string id, CancellationToken cancellationToken)
    {
        if (!OmpProviderId.IsValid(id))
        {
            return new OmpModelsWriteResult(OmpModelsWriteStatus.Rejected, "The provider id is not valid.");
        }

        return await ApplyAsync(text => OmpModelsEditor.Remove(text, id), rollbackOnRejection: false, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteAtomicallyAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)!;
        var temporary = Path.Combine(directory, $"{Path.GetFileName(path)}.autonogrammer-{Guid.NewGuid():N}.tmp");
        try
        {
            await WriteDurablyAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    /// <summary>Creates a new owner-only file (it must not exist), writes the bytes and flushes them to disk.</summary>
    private static async Task WriteDurablyAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = OwnerReadWrite;
        }

        var stream = new FileStream(path, options);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            // .NET has no async flush to disk, so the fsync runs on the thread pool; it must not be cancelled mid-flush.
            await Task.Run(() => stream.Flush(flushToDisk: true), CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leftover temporary file is harmless; the original error is the one worth reporting.
        }
    }

    private async Task<OmpModelsWriteResult> ApplyAsync(Func<string, OmpModelsEdit> edit, bool rollbackOnRejection, CancellationToken cancellationToken)
    {
        await WriteGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(Path.Combine(_agentDirectory, AlternativeFileName)))
            {
                return new OmpModelsWriteResult(
                    OmpModelsWriteStatus.UnsupportedLayout,
                    $"The agent directory has a {AlternativeFileName} file. Autonogrammer cannot tell which of the two files omp reads, so it does not write either. Merge them by hand into {FileName}.");
            }

            byte[]? previous;
            string text;
            try
            {
                previous = File.Exists(FilePath) ? await File.ReadAllBytesAsync(FilePath, cancellationToken).ConfigureAwait(false) : null;
                text = previous is null ? string.Empty : StrictUtf8.GetString(previous);
            }
            catch (DecoderFallbackException)
            {
                return new OmpModelsWriteResult(OmpModelsWriteStatus.UnsupportedLayout, "models.yml is not valid UTF-8. Edit it by hand.");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return Failed($"models.yml could not be read ({exception.GetType().Name}).");
            }

            var decision = edit(text);
            if (decision.Status != OmpModelsWriteStatus.Written)
            {
                return new OmpModelsWriteResult(decision.Status, decision.Message);
            }

            return await WriteAndValidateAsync(decision, previous, rollbackOnRejection, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = WriteGate.Release();
        }
    }

    private async Task<OmpModelsWriteResult> WriteAndValidateAsync(
        OmpModelsEdit decision,
        byte[]? previous,
        bool rollbackOnRejection,
        CancellationToken cancellationToken)
    {
        string? backupPath = null;
        try
        {
            _ = OperatingSystem.IsWindows()
                ? Directory.CreateDirectory(_agentDirectory)
                : Directory.CreateDirectory(_agentDirectory, OwnerOnlyDirectory);
            if (previous is not null)
            {
                backupPath = await CreateBackupAsync(previous, cancellationToken).ConfigureAwait(false);
                RotateBackups();
            }

            await WriteAtomicallyAsync(FilePath, Utf8.GetBytes(decision.NewText!), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed($"models.yml could not be written ({exception.GetType().Name}). The previous file is unchanged.");
        }

        Validation validation;
        try
        {
            validation = await ValidateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _ = await TryRestoreAsync(backupPath, previous).ConfigureAwait(false);
            throw;
        }

        if (validation.Accepted)
        {
            var kept = backupPath is null ? string.Empty : " The previous file was kept as a backup.";
            return new OmpModelsWriteResult(OmpModelsWriteStatus.Written, decision.Message + " omp accepted the file." + kept);
        }

        if (!rollbackOnRejection)
        {
            return new OmpModelsWriteResult(
                OmpModelsWriteStatus.Written,
                $"{decision.Message} omp could not confirm models.yml afterwards: {validation.Reason}");
        }

        var restored = await TryRestoreAsync(backupPath, previous).ConfigureAwait(false);
        if (!restored)
        {
            return Failed($"omp did not accept the change ({validation.Reason}) and the previous models.yml could not be restored. Restore it from the newest {BackupPrefix}* backup.");
        }

        return validation.Rejected
            ? new OmpModelsWriteResult(OmpModelsWriteStatus.Rejected, $"omp rejected the change: {validation.Reason} The previous file was restored.")
            : Failed($"omp could not be asked to check the change ({validation.Reason}), so the previous file was restored.");
    }

    private async Task<Validation> ValidateAsync(CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(new OmpCommandRequest(["models", "--json", "--no-extensions"]), cancellationToken).ConfigureAwait(false);
        if (result.StartFailure is not null)
        {
            return Validation.Unavailable(result.StartFailure);
        }

        if (result.TimedOut)
        {
            return Validation.Unavailable("omp did not answer within the time limit.");
        }

        if (OmpOutputText.ReportsValidationFailure(result.StandardError))
        {
            return Validation.Refused(OmpOutputText.FirstLines(result.StandardError, _options) ?? "models.yml failed validation.");
        }

        return result.ExitCode == 0
            ? Validation.Ok
            : Validation.Unavailable(OmpOutputText.FirstLine(result.StandardError, _options) ?? $"omp exited with code {result.ExitCode}.");
    }

    private async Task<string> CreateBackupAsync(byte[] previous, CancellationToken cancellationToken)
    {
        var stamp = _options.Clock().GetUtcNow().UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        for (var attempt = 1; attempt <= MaxBackupNameAttempts; attempt++)
        {
            var name = attempt == 1 ? BackupPrefix + stamp : $"{BackupPrefix}{stamp}-{attempt}";
            var path = Path.Combine(_agentDirectory, name);
            if (File.Exists(path))
            {
                continue;
            }

            await WriteDurablyAsync(path, previous, cancellationToken).ConfigureAwait(false);
            return path;
        }

        throw new IOException("Too many backups were made within one second.");
    }

    private void RotateBackups()
    {
        try
        {
            var backups = Directory.GetFiles(_agentDirectory, BackupPrefix + "*");
            Array.Sort(backups, StringComparer.Ordinal);
            for (var index = 0; index < backups.Length - BackupsToKeep; index++)
            {
                TryDelete(backups[index]);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Rotation is housekeeping; a failure to list must not fail the write that already succeeded.
        }
    }

    private async Task<bool> TryRestoreAsync(string? backupPath, byte[]? previous)
    {
        try
        {
            if (previous is null)
            {
                File.Delete(FilePath);
                return true;
            }

            var bytes = backupPath is not null && File.Exists(backupPath)
                ? await File.ReadAllBytesAsync(backupPath, CancellationToken.None).ConfigureAwait(false)
                : previous;
            await WriteAtomicallyAsync(FilePath, bytes, CancellationToken.None).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static OmpModelsWriteResult Failed(string message) =>
        new(OmpModelsWriteStatus.Failed, message);

    private readonly record struct Validation(bool Accepted, bool Rejected, string Reason)
    {
        public static Validation Ok => new(Accepted: true, Rejected: false, string.Empty);

        public static Validation Refused(string reason) => new(Accepted: false, Rejected: true, reason);

        public static Validation Unavailable(string reason) => new(Accepted: false, Rejected: false, reason);
    }
}
