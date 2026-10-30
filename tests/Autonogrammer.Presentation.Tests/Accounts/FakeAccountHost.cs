using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Presentation.Accounts;

namespace Autonogrammer.Presentation.Tests.Accounts;

/// <summary>An <see cref="IAccountHost"/> a test sets by hand. It counts the consent action and never throws a transport exception.</summary>
internal sealed class FakeAccountHost : IAccountHost
{
    private AccountHostState _state = AccountHostState.SignedOut;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Gets how many times the consent action was called.</summary>
    public int ConsentCalls { get; private set; }

    /// <summary>Gets how many times cancel was called.</summary>
    public int CancelCalls { get; private set; }

    /// <summary>Gets how many times a code was submitted.</summary>
    public int SubmitCalls { get; private set; }

    /// <inheritdoc />
    public AccountHostState State => _state;

    /// <summary>Sets the state and raises <see cref="Changed"/>.</summary>
    /// <param name="state">The state to show.</param>
    public void Set(AccountHostState state)
    {
        _state = state;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public Task SendCodeAsync(string email, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        Set(AccountHostState.SignedOut with
        {
            Phase = AuthPhase.AwaitingLink,
            Email = email,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
            ResendAvailableAt = DateTimeOffset.UtcNow.AddSeconds(60),
            RemainingAttempts = 5,
        });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResendCodeAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        Set(_state with { Phase = AuthPhase.AwaitingLink, MustResend = false, RemainingAttempts = 5 });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SubmitCodeAsync(string code, CancellationToken cancellationToken)
    {
        _ = code;
        _ = cancellationToken;
        SubmitCalls++;
        Set(_state with { Phase = AuthPhase.SignedIn, Email = _state.Email });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CancelAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        CancelCalls++;
        Set(_state with { Phase = AuthPhase.SignedOut, Error = null });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SignOutAsync(bool everywhere, CancellationToken cancellationToken)
    {
        _ = everywhere;
        _ = cancellationToken;
        Set(AccountHostState.SignedOut);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RememberWithoutSystemProtectionAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        ConsentCalls++;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void ChangeEmail() => Set(AccountHostState.SignedOut with { Configured = _state.Configured });

    /// <inheritdoc />
    public Task RefreshDevicesAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.CompletedTask;
    }
}
