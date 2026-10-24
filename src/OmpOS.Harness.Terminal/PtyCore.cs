using System.Text;
using Porta.Pty;

namespace OmpOS.Harness.Terminal;

/// <summary>
/// The pseudo-terminal machinery shared by <see cref="PtyProcessRunner"/> and <see cref="LinuxLocalPtyBackend"/>:
/// it spawns a command from an argument list in an explicit working directory, reports its exit, reads its output,
/// writes bytes to its input, resizes it and kills it. It makes no decision about what may be written; each caller
/// decides what to send. The one-shot runner sends only its end-of-input byte.
/// </summary>
internal sealed class PtyCore : IDisposable
{
    private const string TerminalName = "OmpOS terminal";

    private readonly IPtyConnection _connection;
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private PtyCore(IPtyConnection connection)
    {
        _connection = connection;
        _connection.ProcessExited += OnExited;
        if (_connection.WaitForExit(0))
        {
            _exited.TrySetResult(_connection.ExitCode);
        }
    }

    /// <summary>Completes with the exit code once the process has exited.</summary>
    public Task<int> Exited => _exited.Task.WaitAsync(CancellationToken.None);

    /// <summary>
    /// Spawns a command in a new pseudo-terminal. No shell is involved: the arguments go to the operating system unchanged.
    /// The command inherits the host environment.
    /// </summary>
    /// <param name="fileName">The executable.</param>
    /// <param name="arguments">The arguments, one element each.</param>
    /// <param name="workingDirectory">The directory the command starts in.</param>
    /// <param name="columns">The number of columns the terminal starts with.</param>
    /// <param name="rows">The number of rows the terminal starts with.</param>
    /// <param name="cancellationToken">Cancels the spawn only. It does not stop the process once it has started.</param>
    public static Task<PtyCore> SpawnAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        int columns,
        int rows,
        CancellationToken cancellationToken)
    {
        return SpawnAsync(fileName, arguments, workingDirectory, columns, rows, null, cancellationToken);
    }

    /// <summary>
    /// Spawns a command in a new pseudo-terminal, with an explicit change set for its environment.
    /// No shell is involved: the arguments go to the operating system unchanged.
    /// </summary>
    /// <param name="fileName">The executable.</param>
    /// <param name="arguments">The arguments, one element each.</param>
    /// <param name="workingDirectory">The directory the command starts in.</param>
    /// <param name="columns">The number of columns the terminal starts with.</param>
    /// <param name="rows">The number of rows the terminal starts with.</param>
    /// <param name="environment">
    /// The variables to set for the command. A variable with an empty value is removed from the inherited environment.
    /// Null leaves the inherited environment unchanged.
    /// </param>
    /// <param name="cancellationToken">Cancels the spawn only. It does not stop the process once it has started.</param>
    public static async Task<PtyCore> SpawnAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        int columns,
        int rows,
        IReadOnlyDictionary<string, string>? environment,
        CancellationToken cancellationToken)
    {
        var options = new PtyOptions
        {
            Name = TerminalName,
            App = fileName,
            CommandLine = arguments.ToArray(),
            Cwd = workingDirectory,
            Cols = columns,
            Rows = rows,
        };
        if (environment is not null)
        {
            options.Environment = new Dictionary<string, string>(environment, StringComparer.Ordinal);
        }

        var connection = await PtyProvider.SpawnAsync(options, cancellationToken).ConfigureAwait(false);
        try
        {
            return new PtyCore(connection);
        }
        catch (Exception)
        {
            // The process is running and nothing else will ever own its connection, so release it before the failure propagates.
            connection.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Changes the size of the pseudo-terminal, which the process sees as its terminal size.
    /// </summary>
    /// <param name="columns">The number of columns. Must be at least 1; the caller validates it.</param>
    /// <param name="rows">The number of rows. Must be at least 1; the caller validates it.</param>
    public void Resize(int columns, int rows)
    {
        _connection.Resize(columns, rows);
    }

    /// <summary>
    /// Writes bytes to the process's input and flushes them.
    /// </summary>
    /// <param name="bytes">The bytes to write.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="IOException">The write fails, for example because the process has exited and no longer reads its input.</exception>
    public async Task WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        var writer = _connection.WriterStream;
        await writer.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the terminal output until it ends, passing each decoded piece to the callback in output order.
    /// The output ends when a read returns no bytes, when the read fails because every process on the terminal has exited,
    /// or when the terminal is disposed while the read is pending.
    /// </summary>
    /// <param name="onOutput">Receives each decoded piece of output.</param>
    public async Task PumpOutputAsync(PtyOutputCallback onOutput)
    {
        var reader = _connection.ReaderStream;
        var decoder = Encoding.UTF8.GetDecoder();
        var bytes = new byte[4096];
        var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];

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
                return;
            }
            catch (ObjectDisposedException)
            {
                // The terminal was disposed while its output was still being read, so there is no more output to deliver.
                return;
            }

            if (read == 0)
            {
                return;
            }

            var count = decoder.GetChars(bytes, 0, read, chars, 0, flush: false);
            if (count > 0)
            {
                onOutput(chars.AsSpan(0, count));
            }
        }
    }

    /// <summary>
    /// Kills the process. An I/O failure from the kill is ignored when the process has already exited.
    /// </summary>
    public void Kill()
    {
        try
        {
            _connection.Kill();
        }
        catch (IOException)
        {
            // The process exited between the request and the kill. Nothing is left to stop.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _connection.ProcessExited -= OnExited;
        _connection.Dispose();
    }

    private void OnExited(object? sender, PtyExitedEventArgs args)
    {
        _exited.TrySetResult(args.ExitCode);
    }
}
