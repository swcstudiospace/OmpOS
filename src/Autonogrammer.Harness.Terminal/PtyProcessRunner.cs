using System.Text;

namespace Autonogrammer.Harness.Terminal;

/// <summary>
/// Runs an approved command in a pseudo-terminal. The executable and the argument list go to the operating system
/// unchanged, with no shell, and the working directory is set explicitly. Output is streamed to the sink as it arrives,
/// and only the first <see cref="ProcessRequest.MaxOutputCharacters"/> characters are kept or streamed; the rest is
/// read and discarded so the command never blocks on a full terminal.
/// </summary>
/// <remarks>
/// A pseudo-terminal merges standard error into the output stream, so <see cref="ProcessOutcome.StandardError"/> is empty.
/// The runner never writes keyboard input to the command. Its only write is an end-of-input character sent once at start,
/// which closes standard input for a command that reads it, as <see cref="SystemProcessRunner"/> does by closing its pipe.
/// The spawn, output, resize and exit machinery is <see cref="PtyCore"/>, which <see cref="LinuxLocalPtyBackend"/> also uses
/// for interactive sessions. Production code calls <see cref="RunAsync"/> only through <see cref="TerminalGate"/>.
/// </remarks>
public sealed class PtyProcessRunner : IProcessRunner
{
    /// <summary>The number of columns a run starts with when no size has been set.</summary>
    public const int DefaultColumns = 80;

    /// <summary>The number of rows a run starts with when no size has been set.</summary>
    public const int DefaultRows = 24;

    private static readonly byte[] EndOfInput = [0x04];

    private readonly IProcessOutputSink? _sink;
    private readonly Lock _sync = new();
    private int _columns = DefaultColumns;
    private int _rows = DefaultRows;
    private PtyCore? _active;

    /// <summary>
    /// Initializes a runner.
    /// </summary>
    /// <param name="sink">Receives the output of each run as it arrives. When null, output is only returned in the outcome.</param>
    public PtyProcessRunner(IProcessOutputSink? sink = null)
    {
        _sink = sink;
    }

    /// <summary>
    /// Sets the pseudo-terminal size. A run that starts later is spawned at this size, and a run in progress is resized to it.
    /// </summary>
    /// <param name="columns">The number of columns. Must be at least 1.</param>
    /// <param name="rows">The number of rows. Must be at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is less than 1.</exception>
    public void Resize(int columns, int rows)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);

        lock (_sync)
        {
            _columns = columns;
            _rows = rows;
            _active?.Resize(columns, rows);
        }
    }

    /// <inheritdoc />
    public async Task<ProcessOutcome> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        int columns;
        int rows;
        lock (_sync)
        {
            columns = _columns;
            rows = _rows;
        }

        using var core = await PtyCore.SpawnAsync(
            request.FileName,
            request.Arguments,
            request.WorkingDirectory,
            columns,
            rows,
            cancellationToken).ConfigureAwait(false);

        try
        {
            lock (_sync)
            {
                _active = core;
            }

            return await CaptureAsync(core, request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_sync)
            {
                _active = null;
            }
        }
    }

    /// <summary>
    /// Sends the end-of-input byte, then reads the command's output until it ends and its exit is known. Output beyond
    /// the request's limit is discarded. Cancellation kills the command, and the run then reports the cancellation.
    /// </summary>
    private async Task<ProcessOutcome> CaptureAsync(PtyCore core, ProcessRequest request, CancellationToken cancellationToken)
    {
        using var stop = cancellationToken.Register(static state => ((PtyCore)state!).Kill(), core);
        await WriteEndOfInputAsync(core).ConfigureAwait(false);

        var captured = new StringBuilder();
        var truncated = false;
        var maxCharacters = request.MaxOutputCharacters;
        var reading = core.PumpOutputAsync(text =>
        {
            var room = maxCharacters - captured.Length;
            if (text.Length > room)
            {
                truncated = true;
            }

            if (room > 0 && text.Length > 0)
            {
                var kept = text[..Math.Min(room, text.Length)];
                captured.Append(kept);
                _sink?.Write(kept.ToString());
            }
        });

        var exitCode = await core.Exited.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        await reading.ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        return new ProcessOutcome(exitCode, captured.ToString(), string.Empty, truncated);
    }

    private static async Task WriteEndOfInputAsync(PtyCore core)
    {
        try
        {
            await core.WriteAsync(EndOfInput, CancellationToken.None).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The command exited before it was written to. It has no input left to close.
        }
    }
}
