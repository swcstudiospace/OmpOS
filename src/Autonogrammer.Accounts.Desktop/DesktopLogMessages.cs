using Microsoft.Extensions.Logging;

namespace Autonogrammer.Accounts.Desktop;

/// <summary>
/// The log lines of the desktop account adapters, EventIds 3070 to 3089. No message carries an email address, code, token, verifier, nonce, path, port or
/// URL query: the only values are fixed reason words, secret-store failure kinds and counts.
/// </summary>
internal static class DesktopLogMessages
{
    private static readonly Action<ILogger, Exception?> LoopbackStartedMessage =
        LoggerMessage.Define(LogLevel.Debug, new EventId(3070, "LoopbackStarted"), "The loopback link listener started.");

    private static readonly Action<ILogger, Exception?> LoopbackBindFailedMessage =
        LoggerMessage.Define(LogLevel.Information, new EventId(3071, "LoopbackBindFailed"), "The loopback link listener could not be started; the code is the only way to sign in.");

    private static readonly Action<ILogger, string, Exception?> LoopbackRequestRejectedMessage =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(3072, "LoopbackRequestRejected"), "A loopback request was rejected: {Reason}.");

    private static readonly Action<ILogger, Exception?> LoopbackCallbackAcceptedMessage =
        LoggerMessage.Define(LogLevel.Information, new EventId(3073, "LoopbackCallbackAccepted"), "The link callback arrived.");

    private static readonly Action<ILogger, Exception?> LoopbackCallbackErrorMessage =
        LoggerMessage.Define(LogLevel.Information, new EventId(3074, "LoopbackCallbackError"), "The link callback reported an error.");

    private static readonly Action<ILogger, Exception?> LoopbackExpiredMessage =
        LoggerMessage.Define(LogLevel.Information, new EventId(3075, "LoopbackExpired"), "The loopback link listener expired without a callback.");

    private static readonly Action<ILogger, Exception?> LoopbackClosedMessage =
        LoggerMessage.Define(LogLevel.Debug, new EventId(3076, "LoopbackClosed"), "The loopback link listener closed.");

    private static readonly Action<ILogger, Exception?> LinkFallbackToCodeMessage =
        LoggerMessage.Define(LogLevel.Information, new EventId(3077, "LinkFallbackToCode"), "The link did not complete; the code is the way to sign in.");

    private static readonly Action<ILogger, Exception?> RefreshLockWaitingMessage =
        LoggerMessage.Define(LogLevel.Debug, new EventId(3079, "RefreshLockWaiting"), "Another holder has the refresh lock; waiting.");

    private static readonly Action<ILogger, Exception?> RefreshLockAcquiredMessage =
        LoggerMessage.Define(LogLevel.Debug, new EventId(3080, "RefreshLockAcquired"), "The refresh lock was acquired.");

    private static readonly Action<ILogger, Exception?> RefreshLockReleasedMessage =
        LoggerMessage.Define(LogLevel.Debug, new EventId(3081, "RefreshLockReleased"), "The refresh lock was released.");

    private static readonly Action<ILogger, Exception?> RefreshLockTimedOutMessage =
        LoggerMessage.Define(LogLevel.Warning, new EventId(3082, "RefreshLockTimedOut"), "The refresh lock stayed held too long; the wait was abandoned.");

    private static readonly Action<ILogger, string, Exception?> SecretStoreDegradedMessage =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(3083, "AccountSecretStoreDegraded"), "The system secret store failed ({Failure}); the session stays in memory only.");

    private static readonly Action<ILogger, Exception?> SecretStoreConsentGrantedMessage =
        LoggerMessage.Define(LogLevel.Information, new EventId(3084, "AccountSecretStoreConsentGranted"), "The person chose to remember the sign-in without system protection.");

    private static readonly Action<ILogger, int, Exception?> SecretStoreMigratedMessage =
        LoggerMessage.Define<int>(LogLevel.Debug, new EventId(3085, "AccountSecretStoreMigrated"), "{Count} in-memory secrets moved to the owner-only file.");

    private static readonly Action<ILogger, Exception?> DeviceKeyRefusedMessage =
        LoggerMessage.Define(LogLevel.Warning, new EventId(3086, "AccountDeviceKeyNotInFile"), "A device key was refused by the file fallback; device keys stay in the system store.");

    private static readonly Action<ILogger, Exception?> FirstLaunchSkippedMessage =
        LoggerMessage.Define(LogLevel.Debug, new EventId(3087, "AccountSecretStoreFirstLaunch"), "No session is stored; the system secret store was not asked.");

    internal static void LoopbackStarted(ILogger? logger) => LoopbackStartedMessage.Log(logger);

    internal static void LoopbackBindFailed(ILogger? logger) => LoopbackBindFailedMessage.Log(logger);

    internal static void LoopbackRequestRejected(ILogger? logger, string reason)
    {
        if (logger is not null)
        {
            LoopbackRequestRejectedMessage(logger, reason, null);
        }
    }

    internal static void LoopbackCallbackAccepted(ILogger? logger) => LoopbackCallbackAcceptedMessage.Log(logger);

    internal static void LoopbackCallbackError(ILogger? logger) => LoopbackCallbackErrorMessage.Log(logger);

    internal static void LoopbackExpired(ILogger? logger) => LoopbackExpiredMessage.Log(logger);

    internal static void LoopbackClosed(ILogger? logger) => LoopbackClosedMessage.Log(logger);

    internal static void LinkFallbackToCode(ILogger? logger) => LinkFallbackToCodeMessage.Log(logger);

    internal static void RefreshLockWaiting(ILogger? logger) => RefreshLockWaitingMessage.Log(logger);

    internal static void RefreshLockAcquired(ILogger? logger) => RefreshLockAcquiredMessage.Log(logger);

    internal static void RefreshLockReleased(ILogger? logger) => RefreshLockReleasedMessage.Log(logger);

    internal static void RefreshLockTimedOut(ILogger? logger) => RefreshLockTimedOutMessage.Log(logger);

    internal static void SecretStoreDegraded(ILogger? logger, string failure)
    {
        if (logger is not null)
        {
            SecretStoreDegradedMessage(logger, failure, null);
        }
    }

    internal static void SecretStoreConsentGranted(ILogger? logger) => SecretStoreConsentGrantedMessage.Log(logger);

    internal static void SecretStoreMigrated(ILogger? logger, int count)
    {
        if (logger is not null)
        {
            SecretStoreMigratedMessage(logger, count, null);
        }
    }

    internal static void DeviceKeyRefused(ILogger? logger) => DeviceKeyRefusedMessage.Log(logger);

    internal static void FirstLaunchSkipped(ILogger? logger) => FirstLaunchSkippedMessage.Log(logger);

    private static void Log(this Action<ILogger, Exception?> message, ILogger? logger)
    {
        if (logger is not null)
        {
            message(logger, null);
        }
    }
}
