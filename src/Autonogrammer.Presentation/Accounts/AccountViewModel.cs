using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Accounts;

/// <summary>
/// The account panel's state. Exactly one of the five phases is visible. Offline, a session that was not remembered, an unconfirmed sign-out and a
/// changed service are notices, never extra phases. Copy comes only from <see cref="AccountCopy"/>.
/// </summary>
public sealed class AccountViewModel : ObservableModel
{
    private readonly IAccountHost _host;
    private readonly TimeProvider _timeProvider;
    private AuthPhase _phase = AuthPhase.SignedOut;
    private string _email = string.Empty;
    private string _code = string.Empty;
    private string? _liveAnnouncement;
    private bool _announced30;
    private bool _announced10;
    private bool _announcedResend;
    private int _lastExpirySeconds = int.MinValue;
    private int _lastResendSeconds = int.MinValue;

    /// <summary>Creates a model over a host.</summary>
    /// <param name="host">The head's account host.</param>
    /// <param name="timeProvider">The clock of the countdowns, or null for the system clock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is null.</exception>
    public AccountViewModel(IAccountHost host, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _host.Changed += OnHostChanged;
        Apply(_host.State, announce: false);
    }

    /// <summary>Gets the one visible phase.</summary>
    public AuthPhase Phase => _phase;

    /// <summary>Gets a value indicating whether the signed-out panel is the one to draw.</summary>
    public bool ShowSignedOut => _phase == AuthPhase.SignedOut;

    /// <summary>Gets a value indicating whether the awaiting-code panel is the one to draw.</summary>
    public bool ShowAwaitingLink => _phase == AuthPhase.AwaitingLink;

    /// <summary>Gets a value indicating whether the signing-in panel is the one to draw.</summary>
    public bool ShowSigningIn => _phase == AuthPhase.SigningIn;

    /// <summary>Gets a value indicating whether the signed-in panel is the one to draw.</summary>
    public bool ShowSignedIn => _phase == AuthPhase.SignedIn;

    /// <summary>Gets a value indicating whether the error panel is the one to draw.</summary>
    public bool ShowError => _phase == AuthPhase.Error;

    /// <summary>Gets how many of the five panels are visible. It is always one.</summary>
    public int VisibleStateCount =>
        (ShowSignedOut ? 1 : 0) + (ShowAwaitingLink ? 1 : 0) + (ShowSigningIn ? 1 : 0) + (ShowSignedIn ? 1 : 0) + (ShowError ? 1 : 0);

    /// <summary>Gets or sets the address the person is typing.</summary>
    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    /// <summary>Gets or sets the code the person is typing. Length is not fixed at 6 digits.</summary>
    public string Code
    {
        get => _code;
        set => SetProperty(ref _code, value);
    }

    /// <summary>Gets the address a code was sent to.</summary>
    public string? AwaitingEmail { get; private set; }

    /// <summary>Gets the sentence for the visible phase.</summary>
    public string PhaseSentence => AccountCopy.For(_phase);

    /// <summary>Gets the sentence for the error, or null when the phase is not the error.</summary>
    public string? ErrorSentence { get; private set; }

    /// <summary>Gets a value indicating whether sign-in is configured.</summary>
    public bool Configured { get; private set; } = true;

    /// <summary>Gets the not-configured sentence when sign-in is not configured, otherwise null.</summary>
    public string? NotConfiguredSentence => Configured ? null : AccountCopy.NotConfigured;

    /// <summary>Gets a value indicating whether the offline notice is shown. The phase stays signed in.</summary>
    public bool ShowOffline { get; private set; }

    /// <summary>Gets the offline notice, or null when it is not shown.</summary>
    public string? OfflineSentence => ShowOffline ? AccountCopy.OfflineNotice : null;

    /// <summary>Gets a value indicating whether the session-not-remembered notice is shown.</summary>
    public bool ShowSessionNotRemembered { get; private set; }

    /// <summary>Gets that notice, or null when it is not shown.</summary>
    public string? SessionNotRememberedSentence => ShowSessionNotRemembered ? AccountCopy.SessionNotRememberedNotice : null;

    /// <summary>Gets a value indicating whether the consent action is offered. It is offered only with the session-not-remembered notice.</summary>
    public bool ShowConsent => ShowSessionNotRemembered;

    /// <summary>Gets a value indicating whether the unconfirmed-sign-out notice is shown.</summary>
    public bool ShowSignOutNotConfirmed { get; private set; }

    /// <summary>Gets that notice, or null when it is not shown.</summary>
    public string? SignOutNotConfirmedSentence => ShowSignOutNotConfirmed ? AccountCopy.SignOutNotConfirmedNotice : null;

    /// <summary>Gets a value indicating whether the service-changed notice is shown.</summary>
    public bool ShowServiceChanged { get; private set; }

    /// <summary>Gets that notice, or null when it is not shown.</summary>
    public string? ServiceChangedSentence => ShowServiceChanged ? AccountCopy.ServiceChangedNotice : null;

    /// <summary>Gets a value indicating whether the Devices row is shown.</summary>
    public bool ShowDevices { get; private set; }

    /// <summary>Gets a value indicating whether another code must be requested before a code can be submitted.</summary>
    public bool MustResend { get; private set; }

    /// <summary>Gets the must-resend sentence, or null when a code may still be tried.</summary>
    public string? MustResendSentence => MustResend ? AccountCopy.MustResend : null;

    /// <summary>Gets a value indicating whether the loopback sentence is shown.</summary>
    public bool ShowLoopbackUnavailable { get; private set; }

    /// <summary>Gets the loopback sentence, or null when a link can return here.</summary>
    public string? LoopbackSentence => ShowLoopbackUnavailable ? AccountCopy.LoopbackUnavailable : null;

    /// <summary>Gets the visible expiry text. It changes every second.</summary>
    public string ExpiryText { get; private set; } = string.Empty;

    /// <summary>Gets the visible resend wait. It changes every second.</summary>
    public string CooldownText { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether another code may be requested now.</summary>
    public bool ResendEnabled { get; private set; }

    /// <summary>Gets the live announcement, or null. It changes only at 30 seconds, 10 seconds and when resend becomes available.</summary>
    public string? LiveAnnouncement => _liveAnnouncement;

    /// <summary>Gets a value indicating whether Cancel applies. It applies while signing in.</summary>
    public bool CanCancel => ShowSigningIn;

    /// <summary>Refreshes the visible countdowns from the clock. The view calls this once a second. Announcements are throttled.</summary>
    public void RefreshClock()
    {
        var state = _host.State;
        int expiry = Remaining(state.ExpiresAt);
        int resend = Remaining(state.ResendAvailableAt);
        ExpiryText = state.ExpiresAt is null ? string.Empty : AccountCopy.ExpiryText(expiry);
        CooldownText = state.ResendAvailableAt is null ? string.Empty : AccountCopy.CooldownText(resend);
        ResendEnabled = ShowAwaitingLink && resend <= 0 && !MustResend;
        string? announcement = null;
        if (ShowAwaitingLink)
        {
            if (expiry == 30 && !_announced30)
            {
                _announced30 = true;
                announcement = AccountCopy.ExpiryIn30Seconds;
            }
            else if (expiry == 10 && !_announced10)
            {
                _announced10 = true;
                announcement = AccountCopy.ExpiryIn10Seconds;
            }

            if (resend <= 0 && _lastResendSeconds > 0 && !_announcedResend)
            {
                _announcedResend = true;
                announcement = AccountCopy.ResendAvailableAnnouncement;
            }
        }

        _lastExpirySeconds = expiry;
        _lastResendSeconds = resend;
        if (announcement is not null)
        {
            _liveAnnouncement = announcement;
        }

        NotifyClock();
    }

    /// <summary>Enter: send a code, submit a code, or leave the error. It does nothing while a resend is required or while signing in.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the action has been applied.</returns>
    public Task SubmitFromEnterAsync(CancellationToken cancellationToken)
    {
        if (ShowSignedOut && Configured)
        {
            return SendCodeAsync(cancellationToken);
        }

        if (ShowAwaitingLink && !MustResend)
        {
            return SubmitCodeAsync(cancellationToken);
        }

        if (ShowError)
        {
            return DismissErrorAsync(cancellationToken);
        }

        return Task.CompletedTask;
    }

    /// <summary>Escape: cancel a sign-in in progress, or leave the code screen.</summary>
    /// <param name="cancellationToken">Cancels the cancel itself only.</param>
    /// <returns>A task that completes when the action has been applied.</returns>
    public Task CancelFromEscapeAsync(CancellationToken cancellationToken)
    {
        if (ShowSigningIn)
        {
            return CancelAsync(cancellationToken);
        }

        if (ShowAwaitingLink)
        {
            ChangeEmail();
        }

        return Task.CompletedTask;
    }

    /// <summary>Sends a code to <see cref="Email"/>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the host has updated.</returns>
    public async Task SendCodeAsync(CancellationToken cancellationToken)
    {
        EnterSigningIn();
        await _host.SendCodeAsync(Email, cancellationToken).ConfigureAwait(true);
        Apply(_host.State, announce: false);
    }

    /// <summary>Requests another code.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the host has updated.</returns>
    public async Task ResendCodeAsync(CancellationToken cancellationToken)
    {
        EnterSigningIn();
        await _host.ResendCodeAsync(cancellationToken).ConfigureAwait(true);
        _announcedResend = false;
        Apply(_host.State, announce: false);
    }

    /// <summary>Submits <see cref="Code"/>.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the host has updated.</returns>
    public async Task SubmitCodeAsync(CancellationToken cancellationToken)
    {
        if (MustResend)
        {
            return;
        }

        EnterSigningIn();
        await _host.SubmitCodeAsync(Code, cancellationToken).ConfigureAwait(true);
        Apply(_host.State, announce: false);
    }

    /// <summary>Cancels the sign-in in progress.</summary>
    /// <param name="cancellationToken">Cancels the cancel itself only.</param>
    /// <returns>A task that completes when the host has updated.</returns>
    public async Task CancelAsync(CancellationToken cancellationToken)
    {
        await _host.CancelAsync(cancellationToken).ConfigureAwait(true);
        Apply(_host.State, announce: false);
    }

    /// <summary>Signs out this device.</summary>
    /// <param name="cancellationToken">Cancels the notice to the service.</param>
    /// <returns>A task that completes when the host has updated.</returns>
    public Task SignOutAsync(CancellationToken cancellationToken) => SignOutCoreAsync(everywhere: false, cancellationToken);

    /// <summary>Signs out every session.</summary>
    /// <param name="cancellationToken">Cancels the notice to the service.</param>
    /// <returns>A task that completes when the host has updated.</returns>
    public Task SignOutEverywhereAsync(CancellationToken cancellationToken) => SignOutCoreAsync(everywhere: true, cancellationToken);

    /// <summary>Applies the consent action. The view offers it only while the session-not-remembered notice is shown.</summary>
    /// <param name="cancellationToken">Cancels the move.</param>
    /// <returns>A task that completes when the host has updated.</returns>
    public async Task RememberWithoutSystemProtectionAsync(CancellationToken cancellationToken)
    {
        if (!ShowConsent)
        {
            return;
        }

        await _host.RememberWithoutSystemProtectionAsync(cancellationToken).ConfigureAwait(true);
        Apply(_host.State, announce: false);
    }

    /// <summary>Returns to the address field.</summary>
    public void ChangeEmail()
    {
        _host.ChangeEmail();
        Code = string.Empty;
        Apply(_host.State, announce: false);
    }

    /// <summary>Leaves the error state for the signed-out address field.</summary>
    /// <param name="cancellationToken">Unused. Present so the view can share the command shape.</param>
    /// <returns>A completed task.</returns>
    public Task DismissErrorAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _host.ChangeEmail();
        Apply(_host.State, announce: false);
        return Task.CompletedTask;
    }

    private async Task SignOutCoreAsync(bool everywhere, CancellationToken cancellationToken)
    {
        await _host.SignOutAsync(everywhere, cancellationToken).ConfigureAwait(true);
        Apply(_host.State, announce: false);
    }

    private void OnHostChanged(object? sender, EventArgs e) => Apply(_host.State, announce: false);

    private void EnterSigningIn()
    {
        _phase = AuthPhase.SigningIn;
        NotifyPhase();
    }

    private void Apply(AccountHostState state, bool announce)
    {
        _ = announce;
        var previous = _phase;
        _phase = state.Phase;
        if (state.Phase == AuthPhase.AwaitingLink && previous != AuthPhase.AwaitingLink)
        {
            _announced30 = false;
            _announced10 = false;
            _announcedResend = false;
            _lastResendSeconds = int.MinValue;
        }

        AwaitingEmail = state.Email;
        Configured = state.Configured;
        ShowOffline = state.Phase == AuthPhase.SignedIn && state.Offline;
        ShowSessionNotRemembered = state.SessionNotRemembered;
        ShowSignOutNotConfirmed = state.SignOutNotConfirmed;
        ShowServiceChanged = state.ServiceChanged;
        ShowDevices = state.DevicesAvailable;
        MustResend = state.MustResend;
        ShowLoopbackUnavailable = state.LoopbackUnavailable;
        ErrorSentence = state.Phase == AuthPhase.Error && state.Error is { } kind ? AccountCopy.For(kind) : null;
        RefreshClock();
        NotifyPhase();
    }

    private int Remaining(DateTimeOffset? at)
    {
        if (at is null)
        {
            return 0;
        }

        var span = at.Value - _timeProvider.GetUtcNow();
        if (span <= TimeSpan.Zero)
        {
            return 0;
        }

        return (int)Math.Ceiling(span.TotalSeconds);
    }

    private void NotifyPhase()
    {
        NotifyPropertyChanged(nameof(Phase));
        NotifyPropertyChanged(nameof(ShowSignedOut));
        NotifyPropertyChanged(nameof(ShowAwaitingLink));
        NotifyPropertyChanged(nameof(ShowSigningIn));
        NotifyPropertyChanged(nameof(ShowSignedIn));
        NotifyPropertyChanged(nameof(ShowError));
        NotifyPropertyChanged(nameof(VisibleStateCount));
        NotifyPropertyChanged(nameof(PhaseSentence));
        NotifyPropertyChanged(nameof(ErrorSentence));
        NotifyPropertyChanged(nameof(Configured));
        NotifyPropertyChanged(nameof(NotConfiguredSentence));
        NotifyPropertyChanged(nameof(ShowOffline));
        NotifyPropertyChanged(nameof(OfflineSentence));
        NotifyPropertyChanged(nameof(ShowSessionNotRemembered));
        NotifyPropertyChanged(nameof(SessionNotRememberedSentence));
        NotifyPropertyChanged(nameof(ShowConsent));
        NotifyPropertyChanged(nameof(ShowSignOutNotConfirmed));
        NotifyPropertyChanged(nameof(SignOutNotConfirmedSentence));
        NotifyPropertyChanged(nameof(ShowServiceChanged));
        NotifyPropertyChanged(nameof(ServiceChangedSentence));
        NotifyPropertyChanged(nameof(ShowDevices));
        NotifyPropertyChanged(nameof(MustResend));
        NotifyPropertyChanged(nameof(MustResendSentence));
        NotifyPropertyChanged(nameof(ShowLoopbackUnavailable));
        NotifyPropertyChanged(nameof(LoopbackSentence));
        NotifyPropertyChanged(nameof(AwaitingEmail));
        NotifyPropertyChanged(nameof(CanCancel));
    }

    private void NotifyClock()
    {
        NotifyPropertyChanged(nameof(ExpiryText));
        NotifyPropertyChanged(nameof(CooldownText));
        NotifyPropertyChanged(nameof(ResendEnabled));
        NotifyPropertyChanged(nameof(LiveAnnouncement));
    }
}
