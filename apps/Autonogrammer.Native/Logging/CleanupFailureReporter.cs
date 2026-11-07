using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Native.Logging;

/// <summary>
/// Reports a cleanup failure captured in a <c>finally</c> block. A failure from a method that completed normally is rethrown. A failure from a
/// method whose body already threw is logged, so the original error keeps propagating.
/// </summary>
internal static class CleanupFailureReporter
{
    private static readonly Action<ILogger, Exception?> CleanupFailed = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(1, nameof(CleanupFailed)),
        "A cleanup step failed while another error was propagating; the original error is kept.");

    /// <summary>Rethrows <paramref name="failure"/> when <paramref name="completed"/>; otherwise logs it to <paramref name="logger"/> when one exists.</summary>
    /// <param name="failure">The first captured cleanup failure, or null.</param>
    /// <param name="completed">True when the guarded body returned normally.</param>
    /// <param name="logger">The logger for the exceptional path, or null.</param>
    internal static void Report(ExceptionDispatchInfo? failure, bool completed, ILogger? logger)
    {
        if (failure is null)
        {
            return;
        }

        if (completed)
        {
            failure.Throw();
        }

        if (logger is not null)
        {
            CleanupFailed(logger, failure.SourceException);
        }
    }
}
