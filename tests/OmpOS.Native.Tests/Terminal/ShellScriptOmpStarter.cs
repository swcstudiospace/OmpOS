using OmpOS.Harness.OmpLauncher;
using OmpOS.Harness.Terminal;
using OmpOS.Native.Views.Terminal;

namespace OmpOS.Native.Tests.Terminal;

/// <summary>
/// An <see cref="IOmpTerminalStarter"/> whose omp is <c>/bin/sh -c script</c> started through the real
/// <see cref="LinuxLocalPtyBackend"/>. It stands in for omp: the script prints markers and waits, exactly like a fake omp
/// executable, but no omp, model or network is involved. Every start runs the same script.
/// </summary>
internal sealed class ShellScriptOmpStarter(string script, string workingDirectory) : IOmpTerminalStarter
{
    private const string Shell = "/bin/sh";

    private readonly LinuxLocalPtyBackend _backend = new();

    /// <summary>Gets the sizes each start was asked for, in order.</summary>
    public List<(int Columns, int Rows)> Sizes { get; } = [];

    /// <inheritdoc />
    public async Task<OmpTerminalLaunchResult> StartAsync(
        int columns,
        int rows,
        IProcessOutputSink output,
        CancellationToken cancellationToken)
    {
        Sizes.Add((columns, rows));
        var session = await _backend.StartAsync(
            new TerminalSessionOptions(Shell, ["-c", script], workingDirectory, columns, rows, output),
            cancellationToken).ConfigureAwait(true);
        return new OmpTerminalStarted(Shell, "fake", session);
    }
}
