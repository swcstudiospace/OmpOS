using System.Runtime.InteropServices;

namespace Autonogrammer.Api.Harness;

/// <summary>
/// Stops the engine and its tools from reading this API process through <c>/proc</c> or attaching a debugger to it. The engine runs model-chosen shell
/// commands as the same user as the API, and the API's environment (database password, token digests, provider keys) is readable through
/// <c>/proc/&lt;pid&gt;/environ</c> by any process of that user while the process is dumpable. <see cref="Apply(ILogger)"/> clears the dumpable flag
/// with <c>prctl(PR_SET_DUMPABLE, 0)</c>: <c>/proc/&lt;pid&gt;/*</c> then belongs to root, and ptrace and <c>process_vm_readv</c> by a same-user
/// process are refused without <c>CAP_SYS_PTRACE</c>. Only Linux has the flag; elsewhere the call does nothing.
/// <para>
/// The call must run before the first engine process is spawned, in practice right after the host is built and before the engine factory is used.
/// The flag is reset by <c>execve</c>, so the engine child, which is started through <c>execve</c>, is dumpable again: the user's debugger can still
/// attach to the engine, and only the API stays protected. Spawning the engine first would leave a window in which it could already have read the API.
/// </para>
/// <para>
/// Side effects: the kernel writes no core dump of the API, and tools that attach to the running API (<c>gdb -p</c>, <c>dotnet-dump collect</c>) fail.
/// Set <c>Hosting:RestrictProcessInspection=false</c> to keep them; the restriction is then logged as off.
/// </para>
/// </summary>
internal static partial class ProcessHardening
{
    /// <summary>The configuration key that turns the restriction on or off. Unset means on outside Development, off in Development.</summary>
    internal const string ConfigurationKey = "Hosting:RestrictProcessInspection";

    /// <summary><c>PR_GET_DUMPABLE</c> from <c>linux/prctl.h</c>.</summary>
    private const int PrGetDumpable = 3;

    /// <summary><c>PR_SET_DUMPABLE</c> from <c>linux/prctl.h</c>.</summary>
    private const int PrSetDumpable = 4;

    /// <summary>
    /// Returns whether the restriction is wanted: the value of <c>Hosting:RestrictProcessInspection</c> when it is a boolean, otherwise true
    /// outside Development and false in Development, so a developer's debugger keeps working.
    /// </summary>
    /// <param name="configuration">The host configuration.</param>
    /// <param name="environment">The host environment.</param>
    /// <returns>True when <see cref="Apply(ILogger)"/> should run.</returns>
    internal static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        return bool.TryParse(configuration[ConfigurationKey], out var configured) ? configured : !environment.IsDevelopment();
    }

    /// <summary>Calls <see cref="Apply(ILogger)"/> when <see cref="IsEnabled"/> says the restriction is wanted, and logs that it is off otherwise.</summary>
    /// <param name="logger">Receives the outcome.</param>
    /// <param name="configuration">The host configuration.</param>
    /// <param name="environment">The host environment.</param>
    internal static void ApplyIfEnabled(ILogger logger, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (IsEnabled(configuration, environment))
        {
            Apply(logger);
            return;
        }

        LogDisabled(logger, ConfigurationKey);
    }

    /// <summary>
    /// Clears the dumpable flag of this process on Linux, so another process of the same user cannot read <c>/proc/&lt;pid&gt;/environ</c> or attach
    /// to it. A failure is logged as a warning and never thrown: the API still starts, only without this protection. Other platforms do nothing.
    /// </summary>
    /// <param name="logger">Receives the outcome.</param>
    public static void Apply(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (!OperatingSystem.IsLinux())
        {
            LogNotApplicable(logger);
            return;
        }

        try
        {
            if (SetDumpable(dumpable: false))
            {
                LogApplied(logger);
                return;
            }

            LogFailed(logger, Marshal.GetLastPInvokeError());
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            LogUnavailable(logger, ex);
        }
    }

    /// <summary>
    /// Sets the dumpable flag of this process with <c>PR_SET_DUMPABLE</c>. <see cref="Apply(ILogger)"/> clears it; a test that must leave the shared
    /// test process as it found it sets it again. Call only on Linux.
    /// </summary>
    /// <param name="dumpable">True to make the process dumpable, false to restrict it.</param>
    /// <returns>True when the kernel accepted the change.</returns>
    /// <exception cref="DllNotFoundException">The C library cannot be loaded.</exception>
    /// <exception cref="EntryPointNotFoundException">The C library has no <c>prctl</c>.</exception>
    internal static bool SetDumpable(bool dumpable) => NativePrctl(PrSetDumpable, dumpable ? 1u : 0u, 0, 0, 0) == 0;

    /// <summary>Reads the dumpable flag of this process with <c>PR_GET_DUMPABLE</c>.</summary>
    /// <returns>True when the process is dumpable; false when it is not; <see langword="null"/> when the flag cannot be read (not Linux, or the call failed).</returns>
    internal static bool? IsDumpable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        try
        {
            var flag = NativePrctl(PrGetDumpable, 0, 0, 0, 0);
            return flag < 0 ? null : flag != 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    [LoggerMessage(EventId = 110, Level = LogLevel.Information, Message = "Process inspection is restricted: this process is no longer dumpable, so other processes of the same user cannot read its /proc entries or attach to it. The engine is started after this call.")]
    private static partial void LogApplied(ILogger logger);

    [LoggerMessage(EventId = 111, Level = LogLevel.Warning, Message = "Process inspection could not be restricted: prctl(PR_SET_DUMPABLE) failed with errno {Errno}. The API runs, but a process of the same user may read its environment through /proc.")]
    private static partial void LogFailed(ILogger logger, int errno);

    [LoggerMessage(EventId = 112, Level = LogLevel.Warning, Message = "Process inspection could not be restricted: the C library call prctl is not available. The API runs, but a process of the same user may read its environment through /proc.")]
    private static partial void LogUnavailable(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 113, Level = LogLevel.Debug, Message = "Process inspection restriction skipped: it applies to Linux only.")]
    private static partial void LogNotApplicable(ILogger logger);

    [LoggerMessage(EventId = 114, Level = LogLevel.Warning, Message = "Process inspection is not restricted because {Key} is false. A process of the same user, including the engine's tools, can read this API's environment through /proc.")]
    private static partial void LogDisabled(ILogger logger, string key);

    // prctl is variadic in C (int prctl(int option, ...)); on the 64-bit Linux ABIs the repository builds for, passing the four unsigned long
    // arguments explicitly is the documented calling form, and the unused ones are zero.
    [DllImport("libc", EntryPoint = "prctl", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int NativePrctl(int option, nuint arg2, nuint arg3, nuint arg4, nuint arg5);
}
