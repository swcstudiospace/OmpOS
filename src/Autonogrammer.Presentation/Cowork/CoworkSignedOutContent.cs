using Autonogrammer.Presentation.Accounts;

namespace Autonogrammer.Presentation.Cowork;

/// <summary>
/// The dock content of a signed-out Cowork head. It names the sentence to show and, when sign-in is available, the action the button runs.
/// Opening the sign-in window is the head's concern; this type carries only the request.
/// </summary>
public sealed class CoworkSignedOutContent
{
    private readonly Action? _signIn;

    /// <summary>Creates the content.</summary>
    /// <param name="state">The signed-out state.</param>
    /// <param name="signIn">The action the button runs. Required when <paramref name="state"/> is <see cref="CoworkSignedOutState.SignInAvailable"/>; null otherwise.</param>
    /// <exception cref="ArgumentException"><paramref name="state"/> is not defined, or <see cref="CoworkSignedOutState.SignInAvailable"/> has no action.</exception>
    public CoworkSignedOutContent(CoworkSignedOutState state, Action? signIn)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentException("The signed-out state is not defined.", nameof(state));
        }

        if (state == CoworkSignedOutState.SignInAvailable && signIn is null)
        {
            throw new ArgumentException("A signed-in-available panel needs the sign-in action.", nameof(signIn));
        }

        State = state;
        _signIn = signIn;
    }

    /// <summary>Gets the signed-out state.</summary>
    public CoworkSignedOutState State { get; }

    /// <summary>Gets the sentence the panel shows.</summary>
    public string Sentence => State == CoworkSignedOutState.SignInAvailable ? CoworkCopy.SignInToUseCowork : AccountCopy.NotConfigured;

    /// <summary>Gets the button label, or null when the panel has no button.</summary>
    public string? ButtonLabel => State == CoworkSignedOutState.SignInAvailable ? CoworkCopy.SignInButtonLabel : null;

    /// <summary>Runs the sign-in action. It does nothing when the panel has no button.</summary>
    public void RequestSignIn() => _signIn?.Invoke();
}
