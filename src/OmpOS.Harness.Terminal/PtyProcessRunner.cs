using System.Text;
using Porta.Pty;

namespace OmpOS.Harness.Terminal;

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
/// The runner is used only through <see cref="TerminalGate"/>; nothing else calls it.
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
    private IPtyConnection? _active;

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

        var options = new PtyOptions
        {
            Name = "OmpOS terminal",
            App = request.FileName,
            CommandLine = request.Arguments.ToArray(),
            Cwd = request.WorkingDirectory,
            Cols = columns,
            Rows = rows,
        };

        using var connection = await PtyProvider.SpawnAsync(options, cancellationToken).ConfigureAwait(false);
        var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnExited(object? sender, PtyExitedEventArgs args) => exited.TrySetResult(args.ExitCode);

        connection.ProcessExited += OnExited;
        try
        {
            lock (_sync)
            {
                _active = connection;
            }

            if (connection.WaitForExit(0))
            {
                exited.TrySetResult(connection.ExitCode);
            }

            using var stop = cancellationToken.Register(static state => KillQuietly((IPtyConnection)state!), connection);
            WriteEndOfInput(connection);

            var reading = ReadOutputAsync(connection.ReaderStream, request.MaxOutputCharacters);
            var exitCode = await exited.Task.ConfigureAwait(false);
            var (output, truncated) = await reading.ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            return new ProcessOutcome(exitCode, output, string.Empty, truncated);
        }
        finally
        {
            connection.ProcessExited -= OnExited;
            lock (_sync)
            {
                _active = null;
            }
        }
    }

    private static void WriteEndOfInput(IPtyConnection connection)
    {
        try
        {
            connection.WriterStream.Write(EndOfInput, 0, EndOfInput.Length);
            connection.WriterStream.Flush();
        }
        catch (IOException)
        {
            // The command exited before it was written to. It has no input left to close.
        }
    }

    private static void KillQuietly(IPtyConnection connection)
    {
        try
        {
            connection.Kill();
        }
        catch (IOException)
        {
            // The process exited between the cancellation and the kill. Nothing is left to stop.
        }
    }

    private async Task<(string Text, bool Truncated)> ReadOutputAsync(Stream reader, int maxCharacters)
    {
        var decoder = Encoding.UTF8.GetDecoder();
        var bytes = new byte[4096];
        var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
        var captured = new StringBuilder();
        var truncated = false;

        while (true)
        {
            int read;
            try
            {
                read = await reader.ReadAsync(bytes.AsMemory(), CancellationToken.None).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // A pseudo-terminal reports the end of its output as an I/O error once every process on the terminal has exited.
                break;
            }

            if (read == 0)
            {
                break;
            }

            var count = decoder.GetChars(bytes, 0, read, chars, 0, flush: false);
            var room = maxCharacters - captured.Length;
            if (count > room)
            {
                truncated = true;
            }

            if (room > 0 && count > 0)
            {
                var kept = Math.Min(room, count);
                captured.Append(chars, 0, kept);
                _sink?.Write(new string(chars, 0, kept));
            }
        }

        return (captured.ToString(), truncated);
    }
}
