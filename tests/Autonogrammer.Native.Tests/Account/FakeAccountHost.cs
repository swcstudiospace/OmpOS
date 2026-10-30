using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Presentation.Accounts;

namespace Autonogrammer.Native.Tests.Account;

/// <summary>An account host a headless test sets by hand.</summary>
internal sealed class FakeAccountHost : IAccountHost
{
    private AccountHostState _state = AccountHostState.SignedOut;

    /// <inheritdoc />
    public event EventHandler? Changed;

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
        _ = email;
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResendCodeAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SubmitCodeAsync(string code, CancellationToken cancellationToken)
    {
        _ = code;
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CancelAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SignOutAsync(bool everywhere, CancellationToken cancellationToken)
    {
        _ = everywhere;
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RememberWithoutSystemProtectionAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void ChangeEmail() => Set(AccountHostState.SignedOut);

    /// <inheritdoc />
    public Task RefreshDevicesAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.CompletedTask;
    }
}
