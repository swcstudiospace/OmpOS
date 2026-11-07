using Autonogrammer.Contracts.Accounts;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Accounts;

/// <summary>
/// The log lines of <see cref="SignInCoordinator"/>, EventIds 3030 to 3049. No message carries an email address, code, token, verifier or URL query:
/// the only values are fixed flags and <see cref="AuthErrorKind"/> members.
/// </summary>
internal static class AccountLogMessages
{
    private static readonly Action<ILogger, bool, bool, Exception?> CodeRequestedMessage =
        LoggerMessage.Define<bool, bool>(
            LogLevel.Information,
            new EventId(3030, "AccountCodeRequested"),
            "A sign-in code was requested (resend: {Resend}, link flow: {Link}).");

    private static readonly Action<ILogger, AuthErrorKind, Exception?> CodeRequestRefusedMessage =
        LoggerMessage.Define<AuthErrorKind>(
            LogLevel.Information,
            new EventId(3031, "AccountCodeRequestRefused"),
            "A sign-in code could not be requested: {Kind}.");

    private static readonly Action<ILogger, int, Exception?> CodeRejectedMessage =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(3032, "AccountCodeRejected"),
            "A sign-in code was rejected; {Wrong} of the allowed attempts are used.");

    private static readonly Action<ILogger, string, Exception?> SignInCompletedMessage =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(3033, "AccountSignInCompleted"),
            "Sign-in completed by {Method}.");

    private static readonly Action<ILogger, string, AuthErrorKind, Exception?> SignInFailedMessage =
        LoggerMessage.Define<string, AuthErrorKind>(
            LogLevel.Information,
            new EventId(3034, "AccountSignInFailed"),
            "Sign-in by {Method} failed: {Kind}.");

    private static readonly Action<ILogger, Exception?> CooldownMessage =
        LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(3035, "AccountResendCooldown"),
            "A new code was requested before the cooldown ended.");

    private static readonly Action<ILogger, Exception?> AttemptResetMessage =
        LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(3036, "AccountAttemptReset"),
            "The sign-in attempt was reset.");

    internal static void CodeRequested(ILogger? logger, bool resend, bool link)
    {
        if (logger is not null)
        {
            CodeRequestedMessage(logger, resend, link, null);
        }
    }

    internal static void CodeRequestRefused(ILogger? logger, AuthErrorKind kind)
    {
        if (logger is not null)
        {
            CodeRequestRefusedMessage(logger, kind, null);
        }
    }

    internal static void CodeRejected(ILogger? logger, int wrong)
    {
        if (logger is not null)
        {
            CodeRejectedMessage(logger, wrong, null);
        }
    }

    internal static void SignInCompleted(ILogger? logger, string method)
    {
        if (logger is not null)
        {
            SignInCompletedMessage(logger, method, null);
        }
    }

    internal static void SignInFailed(ILogger? logger, string method, AuthErrorKind kind)
    {
        if (logger is not null)
        {
            SignInFailedMessage(logger, method, kind, null);
        }
    }

    internal static void Cooldown(ILogger? logger)
    {
        if (logger is not null)
        {
            CooldownMessage(logger, null);
        }
    }

    internal static void AttemptReset(ILogger? logger)
    {
        if (logger is not null)
        {
            AttemptResetMessage(logger, null);
        }
    }
}
