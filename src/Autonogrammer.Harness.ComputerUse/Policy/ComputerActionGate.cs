using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>
/// Gate. Steps follow <see cref="DenialCodeCatalog.GateStepOrder"/>. The first failing step wins.
/// An exception is <see cref="DenialCode.InternalError"/>.
/// </summary>
public sealed class ComputerActionGate : IComputerActionGate
{
    private readonly ComputerUseOptions _options;
    private readonly IComputerUseAccessFacts _access;
    private readonly IComputerObservation _observation;
    private readonly GrantStore _grants;
    private readonly AppCatalogue _catalogue;
    private readonly RateLimiter _rateLimiter;
    private readonly KillSwitchCoordinator _killSwitch;
    private readonly IComputerApprovalPrompt _prompt;
    private readonly IComputerUseAuditSink _audit;
    private readonly TimeProvider _timeProvider;
    private readonly HashSet<string> _optedIn = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _frames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ComputerHostAction> _hostActions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _seenApps = new(StringComparer.Ordinal);
    private int _images;
    /// <summary>Creates the gate.</summary>
    /// <param name="options">Validated options.</param>
    /// <param name="access">Account facts.</param>
    /// <param name="observation">Window, availability and permission reads. This type does not inject.</param>
    /// <param name="grants">Grant store.</param>
    /// <param name="catalogue">Catalogue and denylist.</param>
    /// <param name="rateLimiter">Limiter.</param>
    /// <param name="killSwitch">Kill switch.</param>
    /// <param name="prompt">Local approval prompt.</param>
    /// <param name="audit">Write-ahead audit.</param>
    /// <param name="time">Clock.</param>
    public ComputerActionGate(
        ComputerUseOptions options,
        IComputerUseAccessFacts access,
        IComputerObservation observation,
        GrantStore grants,
        AppCatalogue catalogue,
        RateLimiter rateLimiter,
        KillSwitchCoordinator killSwitch,
        IComputerApprovalPrompt prompt,
        IComputerUseAuditSink audit,
        TimeProvider time)
    {
        _options = options;
        _access = access;
        _observation = observation;
        _grants = grants;
        _catalogue = catalogue;
        _rateLimiter = rateLimiter;
        _killSwitch = killSwitch;
        _prompt = prompt;
        _audit = audit;
        _timeProvider = time;
    }

    /// <summary>Opts a session in.</summary>
    /// <param name="sessionId">Session id.</param>
    public void OptIn(string sessionId) => _optedIn.Add(sessionId);

    /// <summary>Records the session's latest frame id.</summary>
    /// <param name="sessionId">Session id.</param>
    /// <param name="frameId">Frame id.</param>
    public void NoteFrame(string sessionId, string frameId) => _frames[sessionId] = frameId;

    /// <summary>Binds host-built facts to an action hash.</summary>
    /// <param name="actionHash">Action hash.</param>
    /// <param name="action">Host facts.</param>
    public void BindHostAction(ReadOnlyMemory<byte> actionHash, ComputerHostAction action) =>
        _hostActions[Convert.ToHexString(actionHash.Span)] = action;

    /// <summary>Images already captured this session.</summary>
    public int ImagesCaptured => _images;

    /// <summary>Records one captured image against the session budget.</summary>
    public void RecordImage() => _images++;

    /// <inheritdoc/>
    public async ValueTask<ComputerGateDecision> AuthorizeAsync(ComputerActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            return await EvaluateAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            return Deny(DenialCode.InternalError);
        }
        catch (ComputerBackendException)
        {
            return Deny(DenialCode.InternalError);
        }
    }

    private async ValueTask<ComputerGateDecision> EvaluateAsync(ComputerActionRequest request, CancellationToken cancellationToken)
    {
        ComputerGateDenied? step1 = Step1(request);
        if (step1 is not null)
        {
            return step1;
        }

        ComputerGateDenied? step2 = await Step2Async(cancellationToken).ConfigureAwait(false);
        if (step2 is not null)
        {
            return step2;
        }

        ComputerHostAction host = ResolveHost(request);
        ComputerGateDenied? step3 = Step3(request, host);
        if (step3 is not null)
        {
            return step3;
        }

        IReadOnlyList<WindowInfo> windows = await _observation.ListWindowsAsync(cancellationToken).ConfigureAwait(false);
        WindowInfo? target = ResolveTarget(request, windows);
        ComputerGateDenied? step5 = Step5(request, host, target);
        if (step5 is not null)
        {
            return step5;
        }

        ComputerAvailability availability = await _observation.GetAvailabilityAsync(cancellationToken).ConfigureAwait(false);
        if (availability == ComputerAvailability.SecureDesktop)
        {
            return Deny(DenialCode.SecureDesktop);
        }

        ComputerGateDenied? step6 = Step6(request, host);
        if (step6 is not null)
        {
            return step6;
        }

        ComputerGateDenied? step7 = await Step7Async(request, host, target, cancellationToken).ConfigureAwait(false);
        if (step7 is not null)
        {
            return step7;
        }

        if (!await _audit.TryWriteAheadAsync(request, cancellationToken).ConfigureAwait(false))
        {
            return Deny(DenialCode.AuditUnavailable);
        }

        if (target is not null)
        {
            _seenApps.Add(request.SessionId + "\u001f" + target.AppIdentity);
        }

        return new ComputerGateAllowed
        {
            Ticket = new ComputerActionTicket(request.ActionHash, _timeProvider.GetUtcNow()),
        };
    }

    private ComputerGateDenied? Step1(ComputerActionRequest request)
    {
        if (!_options.Enabled || !_access.IsSignedIn || !_access.IsCoworkSession(request.SessionId))
        {
            return Deny(DenialCode.Disabled);
        }

        if (request.Origin == ComputerActionOrigin.Dispatch && !_options.AllowDispatchSessions)
        {
            return Deny(DenialCode.Disabled);
        }

        if (!_optedIn.Contains(request.SessionId))
        {
            return Deny(DenialCode.NotOptedIn);
        }

        if (_killSwitch.State == KillSwitchState.Tripped)
        {
            return Deny(DenialCode.Revoked);
        }

        return null;
    }

    private async ValueTask<ComputerGateDenied?> Step2Async(CancellationToken cancellationToken)
    {
        ComputerAvailability availability = await _observation.GetAvailabilityAsync(cancellationToken).ConfigureAwait(false);
        if (availability is not ComputerAvailability.Available and not ComputerAvailability.SecureDesktop)
        {
            return availability == ComputerAvailability.PermissionMissing
                ? Deny(DenialCode.PermissionMissing)
                : Deny(DenialCode.PlatformUnavailable);
        }

        IReadOnlyList<ComputerPermissionStatus> permissions = await _observation.GetPermissionStatusAsync(cancellationToken).ConfigureAwait(false);
        for (int i = 0; i < permissions.Count; i++)
        {
            if (!permissions[i].Granted)
            {
                return Deny(DenialCode.PermissionMissing);
            }
        }

        if (_killSwitch.State != KillSwitchState.Armed)
        {
            return Deny(DenialCode.KillSwitchUnavailable);
        }

        IReadOnlyList<WindowInfo> windows = await _observation.ListWindowsAsync(cancellationToken).ConfigureAwait(false);
        bool indicator = false;
        for (int i = 0; i < windows.Count; i++)
        {
            if (string.Equals(windows[i].AppIdentity, AppCatalogue.IndicatorIdentity, StringComparison.Ordinal) && !windows[i].Minimized)
            {
                indicator = true;
            }
        }

        return indicator ? null : Deny(DenialCode.IndicatorUnavailable);
    }

    private ComputerGateDenied? Step3(ComputerActionRequest request, ComputerHostAction host)
    {
        if (host.ActionClass == ComputerActionClass.ListWindows)
        {
            return null;
        }

        if (request.FrameId is null || !_frames.TryGetValue(request.SessionId, out string? latest) || !string.Equals(latest, request.FrameId, StringComparison.Ordinal))
        {
            return Deny(DenialCode.StaleFrame);
        }

        if (request.ScaledCoordinates is ModelPoint point && (point.X < 0 || point.Y < 0 || point.X >= _options.LongEdgePx || point.Y >= _options.LongEdgePx))
        {
            return Deny(DenialCode.OutOfBounds);
        }

        return null;
    }

    private ComputerGateDenied? Step5(ComputerActionRequest request, ComputerHostAction host, WindowInfo? target)
    {
        if (host.Chord is not null && IsForbidden(host.Chord))
        {
            // Chord is checked after denylist, own-window, grant and tier so those shadow it.
        }

        if (target is not null && _catalogue.IsDenied(target.AppIdentity))
        {
            return Deny(DenialCode.DeniedApplication);
        }

        if (target is not null && target.IsOwnProcess)
        {
            return Deny(DenialCode.OwnWindow);
        }

        if (target is null || _grants.FindActive(request.SessionId, target.AppIdentity, request.Origin) is not ComputerGrant grant)
        {
            return Deny(DenialCode.NoGrant);
        }

        if (!ComputerGrantPolicy.Allows(grant.Tier, host.ActionClass))
        {
            return Deny(DenialCode.TierTooLow);
        }

        if (host.Chord is not null && IsForbidden(host.Chord))
        {
            return Deny(DenialCode.ForbiddenChord);
        }

        if (target.AppIdentity.Contains(AppCatalogue.ElevatedMarker, StringComparison.Ordinal))
        {
            return Deny(DenialCode.ElevatedTarget);
        }

        return null;
    }

    private ComputerGateDenied? Step6(ComputerActionRequest request, ComputerHostAction host)
    {
        if (host.Actuating)
        {
            RateLimitReason reason = _rateLimiter.TryConsume();
            if (reason == RateLimitReason.StepLimit)
            {
                return Deny(DenialCode.StepLimit);
            }

            if (reason != RateLimitReason.None)
            {
                return Deny(DenialCode.RateLimited);
            }
        }

        if (_killSwitch.TakeoverPaused)
        {
            return Deny(DenialCode.UserTookOver);
        }

        if (host.ActionClass == ComputerActionClass.Screenshot && _images >= _options.MaxImagesPerSession)
        {
            return Deny(DenialCode.ImageBudget);
        }

        _ = request;
        return null;
    }

    private async ValueTask<ComputerGateDenied?> Step7Async(
        ComputerActionRequest request,
        ComputerHostAction host,
        WindowInfo? target,
        CancellationToken cancellationToken)
    {
        if (_prompt is ILocalApproverAvailability availability && !availability.HasLocalApprover)
        {
            return Deny(DenialCode.NoLocalApprover);
        }

        ComputerGrant? grant = target is null ? null : _grants.FindActive(request.SessionId, target.AppIdentity, request.Origin);
        bool first = target is null || !_seenApps.Contains(request.SessionId + "\u001f" + target.AppIdentity);
        SensitivityReason reason = SensitivityClassifier.Classify(
            target?.Title,
            target is not null && _catalogue.IsRaised(target.AppIdentity),
            host.ActionClass,
            grant?.AutoApproveTyping == true,
            first,
            request.Origin,
            host.IsFileDeletion);
        if (reason == SensitivityReason.None)
        {
            return null;
        }

        DateTimeOffset started = _timeProvider.GetUtcNow();
        ComputerApprovalChoice choice = await _prompt.RequestAsync(BuildFacts(request, target), cancellationToken).ConfigureAwait(false);
        if (choice != ComputerApprovalChoice.Deny)
        {
            return null;
        }

        DateTimeOffset finished = _timeProvider.GetUtcNow();
        return finished - started >= ComputerApprovalFacts.Timeout
            ? Deny(DenialCode.ApprovalTimeout)
            : Deny(DenialCode.ApprovalDenied);
    }

    private ComputerHostAction ResolveHost(ComputerActionRequest request)
    {
        if (_hostActions.TryGetValue(Convert.ToHexString(request.ActionHash.Span), out ComputerHostAction? bound))
        {
            return bound;
        }

        ComputerActionClass? mapped = ComputerGrantPolicy.FromToolName(request.ToolName);
        return new ComputerHostAction
        {
            ActionClass = mapped ?? ComputerActionClass.Click,
            Actuating = mapped != ComputerActionClass.ListWindows,
        };
    }

    private static WindowInfo? ResolveTarget(ComputerActionRequest request, IReadOnlyList<WindowInfo> windows)
    {
        if (request.Target?.WindowId is string windowId)
        {
            for (int i = 0; i < windows.Count; i++)
            {
                if (string.Equals(windows[i].Id, windowId, StringComparison.Ordinal))
                {
                    return windows[i];
                }
            }
        }

        if (request.Target?.AppIdentity is string appIdentity)
        {
            for (int i = windows.Count - 1; i >= 0; i--)
            {
                if (string.Equals(windows[i].AppIdentity, appIdentity, StringComparison.OrdinalIgnoreCase))
                {
                    return windows[i];
                }
            }
        }

        return windows.Count == 0 ? null : windows[^1];
    }

    private bool IsForbidden(string chord)
    {
        string canonical = Canonical(chord);
        if (string.Equals(canonical, Canonical(_options.KillChord), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        IReadOnlyList<string> forbidden = ComputerUseOptionsValidator.ForbiddenChords;
        for (int i = 0; i < forbidden.Count; i++)
        {
            if (string.Equals(canonical, Canonical(forbidden[i]), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Canonical(string chord)
    {
        string[] parts = chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i] = parts[i].ToUpperInvariant() switch
            {
                "CONTROL" => "Ctrl",
                "WINDOWS" or "META" or "SUPER" => "Win",
                "COMMAND" => "Cmd",
                _ => parts[i],
            };
        }

        return string.Join('+', parts);
    }

    private static ComputerApprovalFacts BuildFacts(ComputerActionRequest request, WindowInfo? target) => new()
    {
        ApplicationName = target?.AppIdentity ?? "unknown",
        AppIdentity = target?.AppIdentity ?? "unknown",
        SanitizedTitle = Sanitize(target?.Title),
        HostActionWords = request.ToolName,
        HostSanitizedPreview = "0 characters",
        Origin = request.Origin,
    };

    private static string Sanitize(string? title)
    {
        if (string.IsNullOrEmpty(title))
        {
            return string.Empty;
        }

        return title.Length <= 80 ? title : title[..80];
    }

    private static ComputerGateDenied Deny(DenialCode code) => new()
    {
        Code = code,
        UserMessageId = DenialCodeCatalog.UserMessageId(code),
    };
}
