using System.Text;

namespace OmpOS.Harness.Remote;

/// <summary>
/// What one short remote command did: its exit code, what it wrote to stdout as bytes (cut at the cap the caller gave) and to stderr as
/// text. A command that could not run at all (ssh missing, no answer in time) is a result too, never an exception.
/// </summary>
/// <param name="ExitCode">The exit code of the ssh client, which is the exit code of the remote command, or 255 when ssh itself failed; -1 when the process did not start or was stopped for taking too long.</param>
/// <param name="Output">The first bytes the command wrote to stdout, at most the cap the caller gave.</param>
/// <param name="Error">What the ssh client and the command wrote to stderr (bounded).</param>
/// <param name="OutputTruncated">Whether the command wrote more to stdout than the cap.</param>
/// <param name="TimedOut">Whether the process was killed because it ran past its time limit.</param>
/// <param name="StartFailure">Why the ssh client could not be started, or <see langword="null"/>.</param>
public sealed record RemoteExecResult(int ExitCode, ReadOnlyMemory<byte> Output, string Error, bool OutputTruncated, bool TimedOut, string? StartFailure)
{
    private const int SshFailureExitCode = 255;

    /// <summary>Gets whether the command ran and exited with code 0.</summary>
    public bool Succeeded => ExitCode == 0 && !TimedOut && StartFailure is null;

    /// <summary>Gets whether the connection to the remote machine failed: ssh did not start, timed out or reported its own failure (exit code 255).</summary>
    public bool IsConnectionFailure => StartFailure is not null || TimedOut || ExitCode == SshFailureExitCode;

    /// <summary>Decodes the output as UTF-8 text; invalid bytes become U+FFFD.</summary>
    /// <returns>The text.</returns>
    public string OutputText() => Encoding.UTF8.GetString(Output.Span);

    /// <summary>Explains a connection failure in one plain sentence, with the first lines ssh printed.</summary>
    /// <returns>The sentence, for an exception message or a diagnostic.</returns>
    public string DescribeConnectionFailure()
    {
        if (StartFailure is not null)
        {
            return "The ssh client could not be started: " + StartFailure;
        }

        if (TimedOut)
        {
            return "The remote machine did not answer in time.";
        }

        var diagnosis = RemoteDiagnosisCatalog.FromFailure(ExitCode, Error);
        var detail = RemoteDiagnosisCatalog.Excerpt(Error);
        return diagnosis is null
            ? "The remote command failed" + (detail is null ? "." : ": " + detail)
            : diagnosis.Summary + (detail is null ? string.Empty : " " + detail);
    }
}
