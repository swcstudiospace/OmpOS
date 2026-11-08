using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// Installs omp from the npm registry into the managed directory and activates it. The pipeline runs in a fixed order: read
/// the release, check the Bun range, provision Bun, download and verify the tarball, extract it, install dependencies,
/// compile, health-check, move into place, activate, and probe the activated executable. A failed post-activation probe
/// restores the previous good version. Nothing is run from a state whose SHA-256 does not match.
/// </summary>
public sealed class OmpManagedInstaller
{
    private readonly OmpManagedOptions _options;
    private readonly OmpManagedLayout _layout;
    private readonly OmpManagedStateStore _store;
    private readonly OmpVerifiedDownloader _downloader;
    private readonly OmpBunProvisioner _bun;

    /// <summary>Creates the installer.</summary>
    /// <param name="options">The managed options. They are validated here.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null, or a required seam is.</exception>
    /// <exception cref="ArgumentException">An option is malformed.</exception>
    public OmpManagedInstaller(OmpManagedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
        _layout = new OmpManagedLayout(options.RootDirectory);
        _store = new OmpManagedStateStore(_layout.StatePath);
        _downloader = new OmpVerifiedDownloader(options.HttpClient, options.DownloadTimeout);
        _bun = new OmpBunProvisioner(options, _layout, _downloader);
    }

    internal OmpManagedOptions Options => _options;

    internal OmpManagedLayout Layout => _layout;

    internal OmpManagedStateStore Store => _store;

    /// <summary>
    /// Reads the registry's package record and returns the release to install: <paramref name="requestedVersion"/> when given,
    /// otherwise the <c>latest</c> dist-tag.
    /// </summary>
    /// <param name="requestedVersion">The version to read, or null for <c>latest</c>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The release.</returns>
    /// <exception cref="HttpRequestException">The registry could not be reached, or it answered with an error status.</exception>
    /// <exception cref="OperationCanceledException">The network timeout passed, or <paramref name="cancellationToken"/> was cancelled.</exception>
    /// <exception cref="InvalidDataException">The response is not a usable package record.</exception>
    public async Task<OmpRelease> ReadReleaseAsync(string? requestedVersion, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(_options.NetworkTimeout);
        var uri = new Uri(_options.RegistryBaseUri, "@oh-my-pi/pi-coding-agent");
        using var response = await _options.HttpClient.GetAsync(uri, HttpCompletionOption.ResponseContentRead, limit.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadAsByteArrayAsync(limit.Token).ConfigureAwait(false);
        return OmpRegistryDocument.ParseRelease(document, requestedVersion);
    }

    /// <summary>
    /// Installs <paramref name="release"/> and activates it when it passes its probes.
    /// </summary>
    /// <param name="release">The release read by <see cref="ReadReleaseAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the install. A cancelled install removes its staging directory.</param>
    /// <returns>
    /// <see cref="OmpUpdated"/> when the release is active; <see cref="OmpRolledBack"/> when it failed its probe after activation;
    /// <see cref="OmpDeferred"/> when a file was in use; <see cref="OmpOffline"/> when the network failed; otherwise an
    /// <see cref="OmpUpdateFailed"/> whose kind names the failure.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="release"/> is null.</exception>
    public async Task<OmpUpdateResult> InstallAsync(OmpRelease release, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        if (!OmpSemver.TryParse(release.Version, out _))
        {
            return OmpResults.Failed(OmpFailureKind.InvalidRegistryResponse, release.Version, OmpManagedMessages.InvalidVersion(release.Version));
        }

        var loaded = TryLoadState();
        if (loaded.Failure is { } loadFailure)
        {
            return loadFailure;
        }

        var state = loaded.Value!;
        if (state.IsQuarantined(release.Version))
        {
            return OmpResults.Failed(OmpFailureKind.Quarantined, release.Version, OmpManagedMessages.Quarantined(release.Version));
        }

        if (await IsAlreadyActiveAsync(state, release.Version, cancellationToken).ConfigureAwait(false))
        {
            return OmpResults.Failed(OmpFailureKind.Generic, release.Version, OmpManagedMessages.AlreadyActive(release.Version));
        }

        if (CheckEngines(release) is { } engineFailure)
        {
            return engineFailure;
        }

        try
        {
            return await InstallCoreAsync(release, state, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsMappedFailure(exception, cancellationToken))
        {
            return MapFailure(exception, release.Version, state);
        }
    }

    private static bool IsMappedFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException or InvalidDataException or OmpArchiveRejectedException or UnauthorizedAccessException or IOException
        || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    private OmpUpdateResult MapFailure(Exception exception, string version, OmpManagedState state)
    {
        switch (exception)
        {
            case OperationCanceledException:
                return OmpResults.Offline(state);
            case HttpRequestException when OmpFailureMapper.IsTrustFailure(exception):
                return OmpResults.TrustFailure(version);
            case HttpRequestException:
                return OmpResults.Offline(state);
            case OmpArchiveRejectedException:
                return OmpResults.Failed(OmpFailureKind.UnsafeArchive, version, OmpManagedMessages.UnsafeArchive(exception.Message));
            case InvalidDataException:
                return OmpResults.Failed(OmpFailureKind.InvalidArchive, version, OmpManagedMessages.InvalidArchive(exception.Message));
            case UnauthorizedAccessException:
                return OmpResults.Failed(OmpFailureKind.Permission, version, OmpManagedMessages.PermissionDenied(_layout.BaseDirectory));
            default:
                return OmpResults.Failed(OmpFailureKind.AntivirusSuspected, version, OmpManagedMessages.AntivirusBlocked(_layout.BaseDirectory));
        }
    }

    internal OmpStep<OmpManagedState> TryLoadState()
    {
        try
        {
            return OmpStep<OmpManagedState>.Ok(_store.Load());
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return OmpStep<OmpManagedState>.Fail(
                OmpResults.Failed(OmpFailureKind.StateUnreadable, null, OmpManagedMessages.StateUnreadable(_store.StatePath)));
        }
    }

    /// <summary>Reports whether the active executable is missing or does not match its recorded SHA-256.</summary>
    /// <param name="state">The state to check.</param>
    /// <param name="cancellationToken">Cancels the hash.</param>
    /// <returns>True when the active install must not be run.</returns>
    internal async Task<bool> IsActiveBrokenAsync(OmpManagedState state, CancellationToken cancellationToken)
    {
        if (state.ActiveVersion is not { } version)
        {
            return false;
        }

        var executable = _layout.ExecutablePath(version);
        if (!File.Exists(executable) || state.ActiveSha256 is null)
        {
            return true;
        }

        var actual = await OmpHashing.Sha256Async(executable, cancellationToken).ConfigureAwait(false);
        return !string.Equals(actual, state.ActiveSha256, StringComparison.OrdinalIgnoreCase);
    }

    internal bool IsActiveLocked(string version) => _options.FileSystem.IsFileLocked(_layout.ExecutablePath(version));

    internal bool PreviousAvailable(OmpManagedState state) =>
        state.PreviousGood is { } previous && File.Exists(_layout.ExecutablePath(previous));

    internal OmpBroken BrokenResult(OmpManagedState state, string broken)
    {
        var rollback = PreviousAvailable(state);
        return new OmpBroken(broken, rollback, OmpManagedMessages.Broken(broken, rollback));
    }

    /// <summary>Runs <c>omp --version</c> on an executable and requires the version in its output.</summary>
    /// <param name="executable">The executable to probe.</param>
    /// <param name="version">The version the output must contain.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>Null when the probe passed; otherwise the failure.</returns>
    internal async Task<OmpUpdateResult?> ProbeAsync(string executable, string version, CancellationToken cancellationToken)
    {
        var result = await RunAsync(executable, OmpCommands.VersionArguments, WorkingDirectoryOf(executable), _options.HealthCheckTimeout, cancellationToken).ConfigureAwait(false);
        if (result.Outcome == OmpProcessOutcome.StartFailed)
        {
            return ExecutionFailure(result.StartError, version);
        }

        if (result.Outcome == OmpProcessOutcome.TimedOut)
        {
            return OmpResults.Failed(OmpFailureKind.HealthCheckFailed, version, OmpManagedMessages.HealthCheckTimedOut(version));
        }

        if (result.ExitCode != 0 || !result.Output.Contains(version, StringComparison.Ordinal))
        {
            return OmpResults.Failed(OmpFailureKind.HealthCheckFailed, version, OmpManagedMessages.HealthCheckFailed(version, result.Output));
        }

        return null;
    }

    private async Task<bool> IsAlreadyActiveAsync(OmpManagedState state, string version, CancellationToken cancellationToken) =>
        string.Equals(state.ActiveVersion, version, StringComparison.Ordinal)
        && !await IsActiveBrokenAsync(state, cancellationToken).ConfigureAwait(false);

    private async Task<OmpUpdateResult> InstallCoreAsync(OmpRelease release, OmpManagedState state, CancellationToken cancellationToken)
    {
        var bun = await _bun.EnsureAsync(state, cancellationToken).ConfigureAwait(false);
        if (bun.Failure is { } bunFailure)
        {
            return bunFailure;
        }

        var staged = await StageAsync(release, state, bun.Value!, cancellationToken).ConfigureAwait(false);
        if (staged.Failure is { } stageFailure)
        {
            return stageFailure;
        }

        var placed = PlaceStaged(staged.Value!, release.Version);
        if (placed.Failure is { } placeFailure)
        {
            return placeFailure;
        }

        return await ActivateAsync(release.Version, state, cancellationToken).ConfigureAwait(false);
    }

    private OmpUpdateFailed? CheckEngines(OmpRelease release)
    {
        var range = release.EnginesBun;
        if (range is null || !OmpSemver.TryParse(_options.BunPin.Version, out var pinned))
        {
            return null;
        }

        var satisfied = OmpSemver.SatisfiesRange(range, pinned);
        if (satisfied is null)
        {
            return OmpResults.Failed(OmpFailureKind.InvalidRegistryResponse, release.Version, OmpManagedMessages.UnreadableEngines(release.Version, range));
        }

        if (satisfied == false)
        {
            return OmpResults.Failed(
                OmpFailureKind.IncompatibleVersion,
                release.Version,
                OmpManagedMessages.IncompatibleVersion(release.Version, range, _options.BunPin.Version));
        }

        return null;
    }

    private async Task<OmpStep<string>> StageAsync(OmpRelease release, OmpManagedState state, string bunPath, CancellationToken cancellationToken)
    {
        var reusable = FindVerifiedStaging(release.Version);
        if (reusable is not null)
        {
            var reprobe = await ProbeAsync(StagedExecutable(reusable), release.Version, cancellationToken).ConfigureAwait(false);
            if (reprobe is null)
            {
                return OmpStep<string>.Ok(reusable);
            }

            OmpCleanup.DeleteDirectory(reusable);
        }

        PruneStaging();
        var staging = Path.Combine(_layout.StagingDirectory, release.Version + "-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(staging);
        var built = false;
        try
        {
            var failure = await BuildAsync(release, state, bunPath, staging, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return OmpStep<string>.Fail(failure);
            }

            built = true;
            return OmpStep<string>.Ok(staging);
        }
        finally
        {
            if (!built)
            {
                OmpCleanup.DeleteDirectory(staging);
            }
        }
    }

    private async Task<OmpUpdateResult?> BuildAsync(OmpRelease release, OmpManagedState state, string bunPath, string staging, CancellationToken cancellationToken)
    {
        var source = await StageSourceAsync(release, state, staging, cancellationToken).ConfigureAwait(false);
        if (source is not null)
        {
            return source;
        }

        var compiled = await CompileAsync(bunPath, release.Version, staging, cancellationToken).ConfigureAwait(false);
        if (compiled is not null)
        {
            return compiled;
        }

        var health = await ProbeAsync(StagedExecutable(staging), release.Version, cancellationToken).ConfigureAwait(false);
        if (health is not null)
        {
            return health;
        }

        await File.WriteAllTextAsync(Path.Combine(staging, OmpCommands.VerifiedMarker), release.Version, cancellationToken).ConfigureAwait(false);
        return null;
    }

    private async Task<OmpUpdateResult?> StageSourceAsync(OmpRelease release, OmpManagedState state, string staging, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_layout.DownloadsDirectory);
        var part = Path.Combine(_layout.DownloadsDirectory, release.Version + ".part");
        try
        {
            var spec = new OmpDownloadSpec(release.TarballUri, part, HashAlgorithmName.SHA512, release.Sha512Hex);
            var status = await _downloader.DownloadAsync(spec, cancellationToken).ConfigureAwait(false);
            if (OmpResults.ForDownload(status, "omp " + release.Version, release.Version, state) is { } failure)
            {
                return failure;
            }

            OmpTarballExtractor.Extract(part, staging);
            return VerifyPackage(staging, release.Version);
        }
        finally
        {
            OmpCleanup.DeleteFile(part);
        }
    }

    private static OmpUpdateFailed? VerifyPackage(string staging, string version)
    {
        var manifest = Path.Combine(staging, "package.json");
        if (!File.Exists(manifest))
        {
            return OmpResults.Failed(OmpFailureKind.InvalidArchive, version, OmpManagedMessages.InvalidArchive("package.json is missing"));
        }

        try
        {
            using var document = JsonDocument.Parse(new ReadOnlyMemory<byte>(File.ReadAllBytes(manifest)));
            if (DeclaresEntryPoint(document.RootElement))
            {
                return null;
            }

            return OmpResults.Failed(
                OmpFailureKind.InvalidArchive,
                version,
                OmpManagedMessages.InvalidArchive("package.json does not declare bin.omp as " + OmpCommands.EntryPoint));
        }
        catch (JsonException exception)
        {
            return OmpResults.Failed(OmpFailureKind.InvalidArchive, version, OmpManagedMessages.InvalidArchive(exception.Message));
        }
    }

    private static bool DeclaresEntryPoint(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty("bin", out var bin)
        && bin.ValueKind == JsonValueKind.Object
        && bin.TryGetProperty("omp", out var entry)
        && entry.ValueKind == JsonValueKind.String
        && string.Equals(entry.GetString(), OmpCommands.EntryPoint, StringComparison.Ordinal);

    private async Task<OmpUpdateResult?> CompileAsync(string bunPath, string version, string staging, CancellationToken cancellationToken)
    {
        var install = await RunAsync(bunPath, OmpCommands.InstallArguments, staging, _options.ProcessTimeout, cancellationToken).ConfigureAwait(false);
        if (ProcessFailure(install, version, "bun install") is { } installFailure)
        {
            return installFailure;
        }

        var binDirectory = Path.Combine(staging, "bin");
        Directory.CreateDirectory(binDirectory);
        var output = Path.Combine(binDirectory, OmpLocator.ExecutableName);
        var build = await RunAsync(
            bunPath,
            ["build", "--compile", OmpCommands.EntryPoint, "--outfile", output],
            staging,
            _options.ProcessTimeout,
            cancellationToken).ConfigureAwait(false);
        if (ProcessFailure(build, version, "bun build --compile") is { } buildFailure)
        {
            return buildFailure;
        }

        return File.Exists(output)
            ? null
            : OmpResults.Failed(OmpFailureKind.Generic, version, OmpManagedMessages.ToolFailed("bun build --compile", "No executable was written."));
    }

    private OmpUpdateFailed? ProcessFailure(OmpProcessResult result, string version, string step)
    {
        if (result.Outcome == OmpProcessOutcome.Exited && result.ExitCode == 0)
        {
            return null;
        }

        if (result.Outcome == OmpProcessOutcome.StartFailed)
        {
            return ExecutionFailure(result.StartError, version);
        }

        if (result.Outcome == OmpProcessOutcome.TimedOut)
        {
            return OmpResults.Failed(OmpFailureKind.Generic, version, OmpManagedMessages.ToolTimedOut(step));
        }

        return OmpResults.Failed(OmpFailureKind.Generic, version, OmpManagedMessages.ToolFailed(step, result.Output));
    }

    private OmpUpdateFailed ExecutionFailure(Exception? error, string version)
    {
        if (error is UnauthorizedAccessException)
        {
            return OmpResults.Failed(OmpFailureKind.Permission, version, OmpManagedMessages.PermissionDenied(_layout.BaseDirectory));
        }

        return OmpResults.Failed(OmpFailureKind.AntivirusSuspected, version, OmpManagedMessages.AntivirusBlocked(_layout.BaseDirectory));
    }

    private OmpStep<string> PlaceStaged(string staging, string version)
    {
        var destination = _layout.VersionDirectory(version);
        try
        {
            Directory.CreateDirectory(_layout.VersionsDirectory);
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, recursive: true);
            }

            _options.FileSystem.MoveDirectory(staging, destination);
            return OmpStep<string>.Ok(destination);
        }
        catch (UnauthorizedAccessException)
        {
            return OmpStep<string>.Fail(new OmpDeferred(version, OmpManagedMessages.DeferredPermission(_layout.BaseDirectory)));
        }
        catch (IOException)
        {
            return OmpStep<string>.Fail(new OmpDeferred(version, OmpManagedMessages.Deferred(version)));
        }
    }

    private async Task<OmpUpdateResult> ActivateAsync(string version, OmpManagedState state, CancellationToken cancellationToken)
    {
        var previous = string.Equals(state.ActiveVersion, version, StringComparison.Ordinal) ? state.PreviousGood : state.ActiveVersion;
        var executable = _layout.ExecutablePath(version);
        var now = _options.Clock.GetUtcNow();
        var sha = await OmpHashing.Sha256Async(executable, cancellationToken).ConfigureAwait(false);
        _store.Save(new OmpManagedState(version, sha, previous, state.Quarantined, now));

        var probe = await ProbeAsync(executable, version, cancellationToken).ConfigureAwait(false);
        if (probe is null)
        {
            RemoveVersionsExcept(version, previous);
            return new OmpUpdated(version, previous, OmpManagedMessages.Updated(version, previous));
        }

        return await RestoreAfterFailedProbeAsync(version, previous, state, probe.Reason, now, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OmpUpdateResult> RestoreAfterFailedProbeAsync(
        string failed,
        string? previous,
        OmpManagedState state,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var quarantine = new List<OmpQuarantinedVersion>();
        if (state.Quarantined is not null)
        {
            quarantine.AddRange(state.Quarantined);
        }

        quarantine.Add(new OmpQuarantinedVersion(failed, reason, now));
        if (previous is null)
        {
            _store.Save(new OmpManagedState(null, null, null, quarantine, now));
            return OmpResults.Failed(OmpFailureKind.HealthCheckFailed, failed, OmpManagedMessages.NoPreviousVersion(failed));
        }

        var restored = _layout.ExecutablePath(previous);
        var restoredSha = File.Exists(restored) ? await OmpHashing.Sha256Async(restored, cancellationToken).ConfigureAwait(false) : null;
        var previousGood = string.Equals(state.PreviousGood, previous, StringComparison.Ordinal) ? null : state.PreviousGood;
        _store.Save(new OmpManagedState(previous, restoredSha, previousGood, quarantine, now));
        return new OmpRolledBack(failed, previous, OmpManagedMessages.RolledBack(failed, previous));
    }

    private void RemoveVersionsExcept(string keepA, string? keepB)
    {
        if (!Directory.Exists(_layout.VersionsDirectory))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(_layout.VersionsDirectory))
        {
            var name = Path.GetFileName(directory);
            if (!string.Equals(name, keepA, StringComparison.Ordinal) && !string.Equals(name, keepB, StringComparison.Ordinal))
            {
                OmpCleanup.DeleteDirectory(directory);
            }
        }
    }

    private string? FindVerifiedStaging(string version)
    {
        if (!Directory.Exists(_layout.StagingDirectory))
        {
            return null;
        }

        var prefix = version + "-";
        foreach (var directory in Directory.GetDirectories(_layout.StagingDirectory))
        {
            var name = Path.GetFileName(directory);
            if (name.StartsWith(prefix, StringComparison.Ordinal)
                && File.Exists(Path.Combine(directory, OmpCommands.VerifiedMarker))
                && File.Exists(StagedExecutable(directory)))
            {
                return directory;
            }
        }

        return null;
    }

    private void PruneStaging()
    {
        if (!Directory.Exists(_layout.StagingDirectory))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(_layout.StagingDirectory))
        {
            OmpCleanup.DeleteDirectory(directory);
        }
    }

    private static string StagedExecutable(string staging) => Path.Combine(staging, "bin", OmpLocator.ExecutableName);

    private string WorkingDirectoryOf(string executable) => Path.GetDirectoryName(executable) ?? _layout.BaseDirectory;

    private Task<OmpProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory, TimeSpan timeout, CancellationToken cancellationToken) =>
        _options.ProcessRunner.RunAsync(new OmpProcessRequest(fileName, arguments, workingDirectory, timeout), cancellationToken);
}
