using System.Globalization;
using Autonogrammer.Workspace.Conventions;

namespace Autonogrammer.Harness.Remote;

/// <summary>
/// An <see cref="IConventionFileSource"/> over an ssh host: slash commands, skills, rules and subagents are discovered on the remote workspace and
/// in the remote user's home, not on this machine. It reads only inside the roots it was given: a path outside every root, a path with a <c>..</c>
/// segment, or a path that resolves through a symbolic link to somewhere outside the root that holds it is refused with
/// <see cref="ConventionDiagnosticKind.EscapesRoot"/> and never read (the remote machine checks the real path). No member throws for a missing or
/// unreadable path, or for a host that does not answer: the result says what happened. Cancellation does throw. Each call is one ssh process, so a
/// connection-sharing socket (<see cref="SshClientOptions.ControlPathDirectory"/>) makes discovery much faster.
/// </summary>
public sealed class RemoteConventionFileSource : IConventionFileSource
{
    /// <summary>The most entries of one folder the remote machine prints; discovery sorts them and keeps the first ones it was asked for.</summary>
    public const int MaxEntriesPerListing = 10_000;

    private const int ListingBytes = 4 * 1024 * 1024;

    private readonly IRemoteExec _exec;
    private readonly List<string> _roots = [];

    /// <summary>Initializes a new instance of the <see cref="RemoteConventionFileSource"/> class limited to the given roots.</summary>
    /// <param name="exec">Runs the scripts on the remote machine.</param>
    /// <param name="roots">The absolute remote directories reads may stay inside. A value that is not an absolute path is ignored.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exec"/> or <paramref name="roots"/> is null.</exception>
    public RemoteConventionFileSource(IRemoteExec exec, IEnumerable<string> roots)
    {
        ArgumentNullException.ThrowIfNull(exec);
        ArgumentNullException.ThrowIfNull(roots);
        _exec = exec;
        foreach (var root in roots)
        {
            if (RemotePaths.TryNormalize(root, out var normalized) && !_roots.Contains(normalized, StringComparer.Ordinal))
            {
                _roots.Add(normalized);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<ConventionListResult> ListAsync(string directory, int maxEntries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        if (!TryGuard(directory, out var path, out var root, out var refusal))
        {
            return ConventionListResult.Fail(ConventionDiagnosticKind.EscapesRoot, refusal);
        }

        var script = RemoteScripts.List(root, path, MaxEntriesPerListing, verifyLinks: false);
        var result = await _exec.RunAsync(script, ReadOnlyMemory<byte>.Empty, ListingBytes, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return ListFailure(result);
        }

        var (parsed, _) = RemoteFileParsing.ParseList(result.OutputText());
        var entries = parsed.Select(entry => new ConventionEntry(entry.Name, entry.IsDirectory, entry.IsLink)).ToList();
        entries.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
        return new ConventionListResult(entries.Count > maxEntries ? entries.GetRange(0, maxEntries) : entries);
    }

    /// <inheritdoc/>
    public async Task<ConventionReadResult> ReadTextAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!TryGuard(path, out var file, out var root, out var refusal))
        {
            return ConventionReadResult.Fail(ConventionDiagnosticKind.EscapesRoot, refusal);
        }

        var script = RemoteScripts.ReadWithSignature(root, file, maxBytes);
        var outputCap = maxBytes + 1 + RemoteFileParsing.SignatureRoom;
        var result = await _exec.RunAsync(script, ReadOnlyMemory<byte>.Empty, outputCap, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return ReadFailure(result);
        }

        if (!RemoteFileParsing.TrySplitSignedRead(result.Output, out _, out var content))
        {
            return ConventionReadResult.Fail(ConventionDiagnosticKind.Unreadable, "the remote machine gave no answer for the file");
        }

        return Decode(content, maxBytes);
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!TryGuard(path, out var target, out var root, out _))
        {
            return false;
        }

        var result = await _exec.RunAsync(RemoteScripts.Exists(root, target), ReadOnlyMemory<byte>.Empty, RemoteExecLimits.SmallAnswerBytes, cancellationToken).ConfigureAwait(false);
        return result.Succeeded;
    }

    private static ConventionListResult ListFailure(RemoteExecResult result)
    {
        if (result.IsConnectionFailure)
        {
            return ConventionListResult.Fail(ConventionDiagnosticKind.Unreadable, "the remote machine could not be reached: " + result.DescribeConnectionFailure());
        }

        return result.ExitCode switch
        {
            RemoteScripts.ExitRootMissing or RemoteScripts.ExitNotFound or RemoteScripts.ExitNotDirectory => ConventionListResult.Empty,
            RemoteScripts.ExitEscapes or RemoteScripts.ExitUnverifiable => ConventionListResult.Fail(ConventionDiagnosticKind.EscapesRoot, "it is refused: the folder resolves outside the allowed roots"),
            _ => ConventionListResult.Fail(ConventionDiagnosticKind.Unreadable, "the folder cannot be read on the remote machine"),
        };
    }

    private static ConventionReadResult ReadFailure(RemoteExecResult result)
    {
        if (result.IsConnectionFailure)
        {
            return ConventionReadResult.Fail(ConventionDiagnosticKind.Unreadable, "the remote machine could not be reached: " + result.DescribeConnectionFailure());
        }

        return result.ExitCode switch
        {
            RemoteScripts.ExitNotFound => ConventionReadResult.Fail(ConventionDiagnosticKind.Unreadable, "the file does not exist"),
            RemoteScripts.ExitEscapes or RemoteScripts.ExitUnverifiable => ConventionReadResult.Fail(ConventionDiagnosticKind.EscapesRoot, "it is refused: the file resolves outside the allowed roots"),
            RemoteScripts.ExitNotRegular => ConventionReadResult.Fail(ConventionDiagnosticKind.Unreadable, "it is not a regular file"),
            _ => ConventionReadResult.Fail(ConventionDiagnosticKind.Unreadable, "the file cannot be read on the remote machine"),
        };
    }

    private static ConventionReadResult Decode(ReadOnlyMemory<byte> content, int maxBytes)
    {
        if (content.Length > maxBytes)
        {
            return ConventionReadResult.Fail(
                ConventionDiagnosticKind.TooLarge,
                string.Create(CultureInfo.InvariantCulture, $"the file is larger than the {maxBytes / 1024} KiB limit"));
        }

        try
        {
            return ConventionReadResult.Read(RemoteFileParsing.DecodeText(content.Span, out _));
        }
        catch (InvalidDataException)
        {
            return ConventionReadResult.Fail(ConventionDiagnosticKind.Malformed, "the file is not valid UTF-8 text");
        }
    }

    private bool TryGuard(string path, out string normalized, out string root, out string refusal)
    {
        root = string.Empty;
        refusal = "the path is not valid";
        if (!RemotePaths.TryNormalize(path, out normalized))
        {
            return false;
        }

        var holder = RemotePaths.FindContainingRoot(normalized, _roots);
        if (holder is null)
        {
            refusal = "the path is outside the allowed roots";
            return false;
        }

        root = holder;
        return true;
    }
}
