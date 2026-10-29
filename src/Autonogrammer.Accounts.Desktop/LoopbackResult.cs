using Autonogrammer.Harness.Secrets;

namespace Autonogrammer.Accounts.Desktop;

/// <summary>What a loopback link listener found. It never carries query text other than the code, and the code only inside a <see cref="SecretString"/>.</summary>
public sealed class LoopbackResult
{
    private LoopbackResult(LoopbackOutcome outcome, SecretString? code)
    {
        Outcome = outcome;
        Code = code;
    }

    /// <summary>Gets how the listener ended.</summary>
    public LoopbackOutcome Outcome { get; }

    /// <summary>Gets the code of the callback, or null unless <see cref="Outcome"/> is <see cref="LoopbackOutcome.Code"/>.</summary>
    public SecretString? Code { get; }

    /// <summary>Gets a value indicating whether the person has to use the emailed code: the link failed or expired. This is the sign-in error <c>LinkInvalidOrExpired</c>.</summary>
    public bool IsLinkInvalidOrExpired => Outcome != LoopbackOutcome.Code;

    internal static LoopbackResult FromCode(SecretString code) => new(LoopbackOutcome.Code, code);

    internal static LoopbackResult FromLinkError() => new(LoopbackOutcome.LinkError, null);

    internal static LoopbackResult FromExpiry() => new(LoopbackOutcome.Expired, null);

    /// <summary>Names the outcome. The code is never included.</summary>
    /// <returns>The outcome name.</returns>
    public override string ToString() => nameof(LoopbackResult) + ":" + Outcome;
}
