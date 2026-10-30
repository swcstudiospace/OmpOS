using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>
/// Kill switch. Arming is refused until the registrar confirms. A trip releases input, revokes, cancels the turn and audits once.
/// </summary>
public sealed class KillSwitchCoordinator : IKillSwitchControl, IDisposable
{
    private readonly IGlobalHotkeyRegistrar _registrar;
    private readonly IComputerUseBackend _backend;
    private readonly IKillSwitchAudit _audit;
    private readonly Lock _gate = new();
    private CancellationTokenSource _revocation = new();
    private CancellationTokenSource _turn = new();
    private KillSwitchState _state = KillSwitchState.Unregistered;
    private bool _takeover;

    /// <summary>Creates a coordinator.</summary>
    /// <param name="registrar">Hotkey registrar on the backend's own connection.</param>
    /// <param name="backend">Backend used only for <see cref="IComputerUseBackend.ReleaseAllInputAsync"/>.</param>
    /// <param name="audit">Trip audit sink.</param>
    public KillSwitchCoordinator(IGlobalHotkeyRegistrar registrar, IComputerUseBackend backend, IKillSwitchAudit audit)
    {
        _registrar = registrar;
        _backend = backend;
        _audit = audit;
    }

    /// <inheritdoc/>
    public KillSwitchState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary><see langword="true"/> after a pointer takeover until the user resumes.</summary>
    public bool TakeoverPaused
    {
        get
        {
            lock (_gate)
            {
                return _takeover;
            }
        }
    }

    /// <summary>Token cancelled by a trip. Checked between primitives.</summary>
    public CancellationToken RevocationToken
    {
        get
        {
            lock (_gate)
            {
                return _revocation.Token;
            }
        }
    }

    /// <summary>Turn token cancelled by a trip.</summary>
    public CancellationToken TurnToken
    {
        get
        {
            lock (_gate)
            {
                return _turn.Token;
            }
        }
    }

    /// <inheritdoc/>
    public event EventHandler<KillSwitchStateChangedEventArgs>? StateChanged;

    /// <summary>Registers the chord. Returns <see langword="false"/> and stays unregistered when the registrar does not confirm.</summary>
    /// <param name="chord">Kill chord.</param>
    /// <param name="cancellationToken">Token that cancels the call.</param>
    /// <returns><see langword="true"/> when armed.</returns>
    public async ValueTask<bool> TryArmAsync(string chord, CancellationToken cancellationToken)
    {
        bool confirmed = await _registrar.RegisterAsync(chord, cancellationToken).ConfigureAwait(false);
        if (!confirmed)
        {
            return false;
        }

        SetState(KillSwitchState.Armed);
        return true;
    }

    /// <summary>Latches user takeover so later authorisations deny with <see cref="DenialCode.UserTookOver"/>.</summary>
    public void NoteTakeover()
    {
        lock (_gate)
        {
            _takeover = true;
        }
    }

    /// <summary>Clears the takeover latch after the user resumes.</summary>
    public void ResumeAfterTakeover()
    {
        lock (_gate)
        {
            _takeover = false;
        }
    }

    /// <inheritdoc/>
    public async ValueTask TripAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource revocation;
        CancellationTokenSource turn;
        lock (_gate)
        {
            revocation = _revocation;
            turn = _turn;
            _state = KillSwitchState.Tripped;
        }

        await revocation.CancelAsync().ConfigureAwait(false);
        await turn.CancelAsync().ConfigureAwait(false);
        await _backend.ReleaseAllInputAsync(cancellationToken).ConfigureAwait(false);
        _audit.WriteTrip();
        StateChanged?.Invoke(this, new KillSwitchStateChangedEventArgs(KillSwitchState.Tripped));
    }

    /// <inheritdoc/>
    public async ValueTask RearmAsync(CancellationToken cancellationToken)
    {
        bool confirmed = await _registrar.RegisterAsync(string.Empty, cancellationToken).ConfigureAwait(false);
        if (!confirmed)
        {
            return;
        }

        lock (_gate)
        {
            _revocation.Dispose();
            _turn.Dispose();
            _revocation = new CancellationTokenSource();
            _turn = new CancellationTokenSource();
            _takeover = false;
        }

        SetState(KillSwitchState.Armed);
    }

    /// <summary>Releases the revocation and turn sources.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _revocation.Dispose();
            _turn.Dispose();
        }
    }

    private void SetState(KillSwitchState state)
    {
        lock (_gate)
        {
            _state = state;
        }

        StateChanged?.Invoke(this, new KillSwitchStateChangedEventArgs(state));
    }
}
