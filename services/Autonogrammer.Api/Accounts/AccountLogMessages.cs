namespace Autonogrammer.Api.Accounts;

/// <summary>Log messages of the account module. Event ids 3090 to 3099. A message names what failed; it never carries a value.</summary>
internal static class AccountLogMessages
{
    private static readonly Action<ILogger, Exception?> SignupEmailHasNoCodeMessage =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(3090, "AccountSignupEmailHasNoCode"),
            "Magic link is off and the sign-up email has no code; first-time users follow the link-then-code path.");

    private static readonly Action<ILogger, string, Exception?> StoreWriteFailedMessage =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3091, "AccountStoreWriteFailed"),
            "The account store write failed with {ExceptionType}; the request continues.");

    /// <summary>Logs that the sign-up email has no code while the magic link is off.</summary>
    /// <param name="logger">The logger.</param>
    public static void SignupEmailHasNoCode(ILogger logger) => SignupEmailHasNoCodeMessage(logger, null);

    /// <summary>Logs that an account store write failed. The exception type is the only detail.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The failure.</param>
    public static void StoreWriteFailed(ILogger logger, Exception exception) =>
        StoreWriteFailedMessage(logger, exception.GetType().Name, null);
}
