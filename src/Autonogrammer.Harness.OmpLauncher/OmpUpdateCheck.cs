namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// The launch-time check for a newer omp, plus the two recovery actions for a broken active install. The check reads the
/// registry's latest version and installs it when it is newer. Nothing is replaced while the active executable is in use.
/// </summary>
public sealed class OmpUpdateCheck
{
    private readonly OmpManagedInstaller _installer;

    /// <summary>Creates the check.</summary>
    /// <param name="installer">The installer that owns the managed directory.</param>
    /// <exception cref="ArgumentNullException"><paramref name="installer"/> is null.</exception>
    public OmpUpdateCheck(OmpManagedInstaller installer)
    {
        ArgumentNullException.ThrowIfNull(installer);
        _installer = installer;
    }

    /// <summary>
    /// Compares the active version with the registry's latest and installs the latest when it is newer. Call this after first
    /// paint, never on the startup path.
    /// </summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>
    /// <see cref="OmpUpToDate"/> when the active version is the latest or newer; the install's result when a newer version was
    /// installed; <see cref="OmpOffline"/> when the registry cannot be reached; <see cref="OmpDeferred"/> when the active executable
    /// is in use; <see cref="OmpBroken"/> when the active executable does not match its SHA-256; otherwise <see cref="OmpUpdateFailed"/>.
    /// </returns>
    public async Task<OmpUpdateResult> CheckForUpdateAsync(CancellationToken cancellationToken)
    {
        var loaded = _installer.TryLoadState();
        if (loaded.Failure is { } loadFailure)
        {
            return loadFailure;
        }

        var state = loaded.Value!;
        if (state.ActiveVersion is { } broken && await _installer.IsActiveBrokenAsync(state, cancellationToken).ConfigureAwait(false))
        {
            return _installer.BrokenResult(state, broken);
        }

        var fetched = await FetchAsync(state, null, cancellationToken).ConfigureAwait(false);
        if (fetched.Failure is { } fetchFailure)
        {
            return fetchFailure;
        }

        var release = fetched.Value!;
        if (state.ActiveVersion is { } active && !IsNewer(release.Version, active))
        {
            return RecordUpToDate(state, active);
        }

        if (state.ActiveVersion is { } locked && _installer.IsActiveLocked(locked))
        {
            return new OmpDeferred(locked, OmpManagedMessages.ActiveLocked(locked));
        }

        return await _installer.InstallAsync(release, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Recovers a broken active install by reinstalling its version from the registry.</summary>
    /// <param name="cancellationToken">Cancels the reinstall.</param>
    /// <returns>The install's result, or a failure when the active install is not broken.</returns>
    public async Task<OmpUpdateResult> ReinstallBrokenAsync(CancellationToken cancellationToken)
    {
        var loaded = _installer.TryLoadState();
        if (loaded.Failure is { } loadFailure)
        {
            return loadFailure;
        }

        var state = loaded.Value!;
        if (state.ActiveVersion is not { } version || !await _installer.IsActiveBrokenAsync(state, cancellationToken).ConfigureAwait(false))
        {
            return OmpResults.Failed(OmpFailureKind.Generic, null, OmpManagedMessages.NothingToRecover);
        }

        var fetched = await FetchAsync(state, version, cancellationToken).ConfigureAwait(false);
        if (fetched.Failure is { } fetchFailure)
        {
            return fetchFailure;
        }

        return await _installer.InstallAsync(fetched.Value!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Restores the previous good version when the active install is broken. The restored version must pass its probe.</summary>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns><see cref="OmpRolledBack"/> on success; otherwise the failure.</returns>
    public async Task<OmpUpdateResult> RollBackBrokenAsync(CancellationToken cancellationToken)
    {
        var loaded = _installer.TryLoadState();
        if (loaded.Failure is { } loadFailure)
        {
            return loadFailure;
        }

        var state = loaded.Value!;
        if (state.ActiveVersion is not { } broken || !await _installer.IsActiveBrokenAsync(state, cancellationToken).ConfigureAwait(false))
        {
            return OmpResults.Failed(OmpFailureKind.Generic, null, OmpManagedMessages.NothingToRecover);
        }

        if (!_installer.PreviousAvailable(state) || state.PreviousGood is not { } previous)
        {
            return OmpResults.Failed(OmpFailureKind.Generic, broken, OmpManagedMessages.NoPreviousGood);
        }

        var executable = _installer.Layout.ExecutablePath(previous);
        var probe = await _installer.ProbeAsync(executable, previous, cancellationToken).ConfigureAwait(false);
        if (probe is not null)
        {
            return probe;
        }

        var sha = await OmpHashing.Sha256Async(executable, cancellationToken).ConfigureAwait(false);
        _installer.Store.Save(new OmpManagedState(previous, sha, null, state.Quarantined, _installer.Options.Clock.GetUtcNow()));
        return new OmpRolledBack(broken, previous, OmpManagedMessages.RolledBackFromBroken(broken, previous));
    }

    private async Task<OmpStep<OmpRelease>> FetchAsync(OmpManagedState state, string? requestedVersion, CancellationToken cancellationToken)
    {
        try
        {
            return OmpStep<OmpRelease>.Ok(await _installer.ReadReleaseAsync(requestedVersion, cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OmpStep<OmpRelease>.Fail(OmpResults.Offline(state));
        }
        catch (HttpRequestException exception) when (OmpFailureMapper.IsTrustFailure(exception))
        {
            return OmpStep<OmpRelease>.Fail(OmpResults.TrustFailure(requestedVersion));
        }
        catch (HttpRequestException)
        {
            return OmpStep<OmpRelease>.Fail(OmpResults.Offline(state));
        }
        catch (InvalidDataException exception)
        {
            return OmpStep<OmpRelease>.Fail(
                OmpResults.Failed(OmpFailureKind.InvalidRegistryResponse, requestedVersion, OmpManagedMessages.InvalidRegistryResponse(exception.Message)));
        }
    }

    private OmpUpToDate RecordUpToDate(OmpManagedState state, string version)
    {
        var now = _installer.Options.Clock.GetUtcNow();
        try
        {
            _installer.Store.Save(state with { LastCheckedAt = now });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The timestamp is advisory. A failed write leaves the active install and its record as they were.
        }

        return new OmpUpToDate(version, now, OmpManagedMessages.UpToDate(version));
    }

    private static bool IsNewer(string candidate, string installed)
    {
        if (!OmpSemver.TryParse(candidate, out var next) || !OmpSemver.TryParse(installed, out var current))
        {
            return !string.Equals(candidate, installed, StringComparison.Ordinal);
        }

        return OmpSemver.Compare(next, current) > 0;
    }
}
