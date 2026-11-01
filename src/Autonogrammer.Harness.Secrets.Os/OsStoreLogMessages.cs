using Microsoft.Extensions.Logging;

namespace Autonogrammer.Harness.Secrets.Os;

/// <summary>
/// Log messages of the operating system secret stores, EventIds 3050 to 3069. Parameters are store names, operation names, failure kinds and
/// durations: never a secret name, a value or a native error message.
/// </summary>
internal static class OsStoreLogMessages
{
    private static readonly Action<ILogger, string, Exception?> StoreSelectedMessage =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(3050, nameof(StoreSelected)), "Operating system secret store selected: {Store}.");

    private static readonly Action<ILogger, Exception?> StoreUnsupportedMessage =
        LoggerMessage.Define(LogLevel.Warning, new EventId(3051, nameof(StoreUnsupported)), "No operating system secret store exists for this platform.");

    private static readonly Action<ILogger, string, string, long, Exception?> OperationCompletedMessage =
        LoggerMessage.Define<string, string, long>(LogLevel.Debug, new EventId(3052, nameof(OperationCompleted)), "{Store} {Operation} completed in {ElapsedMs} ms.");

    private static readonly Action<ILogger, string, string, SecretStoreFailure, Exception?> OperationFailedMessage =
        LoggerMessage.Define<string, string, SecretStoreFailure>(LogLevel.Warning, new EventId(3053, nameof(OperationFailed)), "{Store} {Operation} failed: {Failure}.");

    public static void StoreSelected(ILogger? logger, string store)
    {
        if (logger is not null)
        {
            StoreSelectedMessage(logger, store, null);
        }
    }

    public static void StoreUnsupported(ILogger? logger)
    {
        if (logger is not null)
        {
            StoreUnsupportedMessage(logger, null);
        }
    }

    public static void OperationCompleted(ILogger? logger, string store, string operation, long elapsedMilliseconds)
    {
        if (logger is not null)
        {
            OperationCompletedMessage(logger, store, operation, elapsedMilliseconds, null);
        }
    }

    public static void OperationFailed(ILogger? logger, string store, string operation, SecretStoreFailure failure)
    {
        if (logger is not null)
        {
            OperationFailedMessage(logger, store, operation, failure, null);
        }
    }
}
