using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using Autonogrammer.Harness.Terminal;

namespace Autonogrammer.Harness.OmpLauncher.Tests.Live;

/// <summary>
/// The output sink of the live terminal test. omp's TUI asks the terminal for its device attributes (<c>ESC [ c</c>) before it
/// draws and waits for the answer, which a real terminal emulator gives. This sink plays that emulator: it answers every query
/// with <c>ESC [ ? 62 ; 22 c</c> (the answer the PROBE-2 run used), through the session it is attached to. It keeps only a
/// bounded tail of the output, for a failure message, and types nothing else: no message ever reaches omp through it.
/// </summary>
internal sealed class LiveTerminalSink : IProcessOutputSink, IAsyncDisposable
{
    /// <summary>The answer to a primary device attributes query: a VT220-class terminal with ANSI colour.</summary>
    public const string DeviceAttributesReply = "\u001b[?62;22c";

    private const char Escape = '\u001b';
    private const string ShortQuery = "[c";
    private const string LongQuery = "[0c";
    private const int RetainedCharacters = 4000;
    private const int FailureTailCharacters = 1500;
    private static readonly TimeSpan QuietPollInterval = TimeSpan.FromMilliseconds(100);

    private readonly Lock _gate = new();
    private readonly StringBuilder _tail = new();
    private readonly Channel<string> _replies = Channel.CreateUnbounded<string>();
    private readonly TaskCompletionSource<ITerminalSession> _attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _pump;
    private string _carry = string.Empty;
    private int _queries;
    private long _lastWriteTimestamp = Stopwatch.GetTimestamp();

    /// <summary>Creates the sink and starts its reply pump. The pump waits until <see cref="Attach"/> is called.</summary>
    public LiveTerminalSink()
    {
        _pump = PumpAsync();
    }

    /// <summary>Gets the number of device attributes queries omp has sent so far.</summary>
    public int DeviceAttributesQueries
    {
        get
        {
            lock (_gate)
            {
                return _queries;
            }
        }
    }

    /// <summary>Gives the sink the session its replies are written to. Queries that arrived earlier are answered then.</summary>
    /// <param name="session">The running terminal session.</param>
    public void Attach(ITerminalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _attached.TrySetResult(session);
    }

    /// <summary>
    /// Waits until omp has written nothing for <paramref name="quiet"/>, which is when its TUI has finished drawing and reads keys,
    /// or until <paramref name="timeout"/> passes.
    /// </summary>
    /// <param name="quiet">How long the output must stay silent.</param>
    /// <param name="timeout">The longest this call waits.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>True when the output went quiet; false when the timeout passed first.</returns>
    public async Task<bool> WaitForQuietAsync(TimeSpan quiet, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started) < timeout)
        {
            if (Stopwatch.GetElapsedTime(Interlocked.Read(ref _lastWriteTimestamp)) >= quiet)
            {
                return true;
            }

            await Task.Delay(QuietPollInterval, cancellationToken).ConfigureAwait(true);
        }

        return false;
    }

    /// <inheritdoc />
    public void Write(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Interlocked.Exchange(ref _lastWriteTimestamp, Stopwatch.GetTimestamp());
        int found;
        lock (_gate)
        {
            found = CountQueries(_carry + text, out var carry);
            _carry = carry;
            _queries += found;
            _tail.Append(text);
            if (_tail.Length > RetainedCharacters)
            {
                _tail.Remove(0, _tail.Length - RetainedCharacters);
            }
        }

        for (var index = 0; index < found; index++)
        {
            _replies.Writer.TryWrite(DeviceAttributesReply);
        }
    }

    /// <summary>Returns the end of the output with every control character removed, for a failure message.</summary>
    /// <returns>Printable text and line breaks only.</returns>
    public string ReadableTail()
    {
        string tail;
        lock (_gate)
        {
            tail = _tail.ToString();
        }

        var readable = new StringBuilder(tail.Length);
        foreach (var character in tail)
        {
            if (character == '\n' || !char.IsControl(character))
            {
                readable.Append(character);
            }
        }

        return readable.Length <= FailureTailCharacters ? readable.ToString() : readable.ToString(readable.Length - FailureTailCharacters, FailureTailCharacters);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _replies.Writer.TryComplete();
        await _stop.CancelAsync().ConfigureAwait(true);
#pragma warning disable VSTHRD003 // The pump is started by this sink's own constructor, so awaiting it cannot wait on work from another context.
        await _pump.ConfigureAwait(true);
#pragma warning restore VSTHRD003
        _stop.Dispose();
    }

    /// <summary>
    /// Counts the complete queries in the text. A query cut at the end of the text is returned in <paramref name="carry"/> so the
    /// next chunk completes it, and it is not counted twice.
    /// </summary>
    private static int CountQueries(string text, out string carry)
    {
        carry = string.Empty;
        var count = 0;
        var index = 0;
        while (true)
        {
            var escape = text.IndexOf(Escape, index);
            if (escape < 0)
            {
                return count;
            }

            var rest = text.AsSpan(escape + 1);
            if (rest.StartsWith(ShortQuery, StringComparison.Ordinal))
            {
                count++;
                index = escape + 1 + ShortQuery.Length;
            }
            else if (rest.StartsWith(LongQuery, StringComparison.Ordinal))
            {
                count++;
                index = escape + 1 + LongQuery.Length;
            }
            else if (rest.Length < LongQuery.Length && LongQuery.AsSpan().StartsWith(rest, StringComparison.Ordinal))
            {
                carry = text[escape..];
                return count;
            }
            else
            {
                index = escape + 1;
            }
        }
    }

    private async Task PumpAsync()
    {
        try
        {
            var session = await _attached.Task.WaitAsync(_stop.Token).ConfigureAwait(true);
            await foreach (var reply in _replies.Reader.ReadAllAsync(_stop.Token).ConfigureAwait(true))
            {
                await session.WriteInputAsync(reply, _stop.Token).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // The sink was disposed: unanswered queries are dropped with the session.
        }
        catch (ObjectDisposedException)
        {
            // The session was disposed while a reply waited.
        }
        catch (IOException)
        {
            // The process has exited and no longer reads its input.
        }
    }
}
