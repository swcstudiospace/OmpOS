using System.Runtime.InteropServices;

namespace Autonogrammer.Harness.Isolation;

/// <summary>
/// Windows process containment: kill on close, no breakaway, and a memory ceiling. This phase does not assign the job.
/// A job object is applied after <c>Process.Start</c>, which <see cref="Autonogrammer.Harness.Abstractions.IProcessLaunchWrapper"/>
/// cannot express, so the planner reports <c>platform_unsupported</c> and the chip says process containment is not applied.
/// The native calls compile on Linux and are not invoked here.
/// </summary>
public sealed class JobObjectContainer
{
    /// <summary><c>JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE</c>.</summary>
    public const uint KillOnJobCloseFlag = 0x2000;

    /// <summary><c>JOB_OBJECT_LIMIT_BREAKAWAY_OK</c>. No-breakaway means this flag is not set.</summary>
    public const uint BreakawayOkFlag = 0x800;

    /// <summary><c>JOB_OBJECT_LIMIT_PROCESS_MEMORY</c>.</summary>
    public const uint ProcessMemoryFlag = 0x100;

    /// <summary><c>JobObjectExtendedLimitInformation</c>.</summary>
    private const int JobObjectExtendedLimitInformation = 9;
    private readonly long _memoryCeilingBytes;
    private readonly bool _killOnClose;
    private readonly bool _noBreakaway;

    /// <summary>Initializes a new instance of the <see cref="JobObjectContainer"/> class.</summary>
    /// <param name="memoryCeilingBytes">The per-process memory ceiling, in bytes. It must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="memoryCeilingBytes"/> is not positive.</exception>
    public JobObjectContainer(long memoryCeilingBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(memoryCeilingBytes);
        _memoryCeilingBytes = memoryCeilingBytes;
        _killOnClose = true;
        _noBreakaway = true;
    }

    /// <summary>Gets a value indicating whether closing the job kills every process in it.</summary>
    public bool KillOnClose => _killOnClose;

    /// <summary>Gets a value indicating whether a child is forbidden from breaking away from the job.</summary>
    public bool NoBreakaway => _noBreakaway;

    /// <summary>Gets the memory ceiling in bytes.</summary>
    public long MemoryCeilingBytes => _memoryCeilingBytes;

    /// <summary>Gets the limit flags this container would set: kill on close and a process memory ceiling, and not breakaway.</summary>
    public uint LimitFlags => (_killOnClose ? KillOnJobCloseFlag : 0) | ProcessMemoryFlag;

    /// <summary>
    /// Assigns <paramref name="processHandle"/> to a new job. Not used in this phase. On a non-Windows host it throws
    /// <see cref="PlatformNotSupportedException"/> before any native call.
    /// </summary>
    /// <param name="processHandle">An open process handle.</param>
    /// <exception cref="PlatformNotSupportedException">The host is not Windows.</exception>
    /// <exception cref="InvalidOperationException">The job could not be created or assigned.</exception>
    public void Assign(nint processHandle)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("A job object is a Windows process containment. It is not applied on this host.");
        }

        var job = CreateJobObject(nint.Zero, null);
        if (job == nint.Zero)
        {
            throw new InvalidOperationException("CreateJobObject failed.");
        }

        var limits = new ExtendedLimits
        {
            Basic = new BasicLimits { LimitFlags = LimitFlags },
            ProcessMemoryLimit = (nuint)_memoryCeilingBytes,
        };
        var size = Marshal.SizeOf<ExtendedLimits>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(limits, buffer, false);
            if (SetInformationJobObject(job, JobObjectExtendedLimitInformation, buffer, (uint)size) == 0
                || AssignProcessToJobObject(job, processHandle) == 0)
            {
                throw new InvalidOperationException("The job object could not be assigned.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimits
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimits
    {
        public BasicLimits Basic;
        public nuint IoReadOperationCount;
        public nuint IoWriteOperationCount;
        public nuint IoOtherOperationCount;
        public nuint IoReadTransferCount;
        public nuint IoWriteTransferCount;
        public nuint IoOtherTransferCount;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint CreateJobObject(nint attributes, string? name);

    [DllImport("kernel32.dll", EntryPoint = "SetInformationJobObject", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int SetInformationJobObject(nint job, int informationClass, nint information, uint length);

    [DllImport("kernel32.dll", EntryPoint = "AssignProcessToJobObject", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int AssignProcessToJobObject(nint job, nint process);
}
