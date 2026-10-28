using System.ComponentModel;
using System.Globalization;
using System.Text;
using OmpOS.Harness.Terminal;
using OmpOS.Presentation.Mentions;

namespace OmpOS.Native.Composition.Mentions;

/// <summary>
/// The git diffs a mention attaches, read with the <c>git</c> command line in the workspace root. The <b>working</b> diff is <c>git diff HEAD</c> plus every untracked file shown as a new
/// file (at most <see cref="MaxUntrackedFiles"/> of them). The <b>branch</b> diff is the merge base of <c>HEAD</c> with the base branch against <c>HEAD</c>: the commits of the branch, not its
/// uncommitted changes. The base branch is <c>origin/HEAD</c> when the clone has it, else the first that exists of <c>origin/main</c>, <c>origin/master</c>, <c>main</c> and <c>master</c>.
/// Both diffs are limited to the workspace folder (<c>-- .</c>). Git runs without a shell, a pager, optional index locks, external diff programs, text conversion filters or a file system monitor,
/// so reading a diff never runs a program the repository names. A folder that is not a repository, a repository with no commit, a missing base branch, a missing git and a slow git
/// all read as <see cref="MentionUnavailableException"/> (the summary reports them as unavailable); nothing here throws anything else except cancellation.
/// </summary>
/// <remarks>When the runner drops output past the byte cap, the reported size is the size of what was kept and <see cref="MentionContent.Truncated"/> is set.</remarks>
internal sealed class GitMentionDiffSource : IGitDiffSource
{
    /// <summary>The most untracked files a working diff shows in full.</summary>
    internal const int MaxUntrackedFiles = 100;

    private const int ListingCharacters = 1024 * 1024;
    private const int ShortCharacters = 16 * 1024;

    private static readonly string[] GlobalOptions =
    [
        "--no-pager",
        "--no-optional-locks",
        "-c",
        "core.fsmonitor=false",
        "-c",
        "core.quotepath=false",
    ];

    private static readonly string[] DiffOptions = ["--no-ext-diff", "--no-textconv", "--no-color"];

    private static readonly string[] BaseCandidates = ["origin/main", "origin/master", "main", "master"];

    private readonly string _root;
    private readonly IProcessRunner _runner;
    private readonly string _git;
    private readonly TimeSpan _timeout;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the source. It starts no process until it is used.</summary>
    /// <param name="workspaceRoot">The workspace folder git runs in.</param>
    /// <param name="runner">Runs git, or <see langword="null"/> for <see cref="SystemProcessRunner"/>.</param>
    /// <param name="gitExecutable">The git executable name or path.</param>
    /// <param name="timeout">How long one git command may run, 30 seconds by default.</param>
    /// <param name="timeProvider">The clock of the timeout, or <see langword="null"/> for the system clock.</param>
    /// <exception cref="ArgumentException"><paramref name="workspaceRoot"/> or <paramref name="gitExecutable"/> is null, empty or white space.</exception>
    internal GitMentionDiffSource(
        string workspaceRoot,
        IProcessRunner? runner = null,
        string gitExecutable = "git",
        TimeSpan? timeout = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(gitExecutable);

        _root = Path.GetFullPath(workspaceRoot);
        _runner = runner ?? new SystemProcessRunner();
        _git = gitExecutable;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async Task<MentionGitDiffSummary> GetSummaryAsync(MentionGitDiffKind kind, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureCommitAsync(cancellationToken).ConfigureAwait(false);
            if (kind == MentionGitDiffKind.Working)
            {
                var tracked = await CountNumstatAsync(["HEAD"], cancellationToken).ConfigureAwait(false);
                var untracked = await ListUntrackedAsync(cancellationToken).ConfigureAwait(false);
                return new MentionGitDiffSummary(true, tracked + untracked.Length, null, null);
            }

            var baseRef = await ResolveBaseAsync(cancellationToken).ConfigureAwait(false);
            var mergeBase = await MergeBaseAsync(baseRef, cancellationToken).ConfigureAwait(false);
            var changed = await CountNumstatAsync([mergeBase, "HEAD"], cancellationToken).ConfigureAwait(false);
            return new MentionGitDiffSummary(true, changed, baseRef, null);
        }
        catch (MentionUnavailableException exception)
        {
            return new MentionGitDiffSummary(false, 0, null, exception.Message);
        }
    }

    /// <inheritdoc/>
    /// <exception cref="MentionUnavailableException">The folder is not a repository, has no commit or no base branch, git is missing or too slow, or git failed.</exception>
    public async Task<MentionContent> ReadAsync(MentionGitDiffKind kind, int maxBytes, CancellationToken cancellationToken)
    {
        await EnsureCommitAsync(cancellationToken).ConfigureAwait(false);
        if (kind == MentionGitDiffKind.Working)
        {
            return await ReadWorkingAsync(maxBytes, cancellationToken).ConfigureAwait(false);
        }

        var baseRef = await ResolveBaseAsync(cancellationToken).ConfigureAwait(false);
        var mergeBase = await MergeBaseAsync(baseRef, cancellationToken).ConfigureAwait(false);
        var diff = await RunCheckedAsync(["diff", .. DiffOptions, mergeBase, "HEAD", "--", "."], maxBytes, "Git could not produce the branch diff", cancellationToken).ConfigureAwait(false);
        var cut = MentionTextCut.Cut(diff.StandardOutput, maxBytes);
        return diff.StandardOutput.Length == 0 && !diff.StandardOutputTruncated
            ? NoChanges("This branch has no changes against " + baseRef + ".")
            : cut with { Truncated = cut.Truncated || diff.StandardOutputTruncated };
    }

    private static MentionContent NoChanges(string sentence) => new(sentence, Encoding.UTF8.GetByteCount(sentence), false);

    private static string FirstLine(string text) => text.Split('\n', 2, StringSplitOptions.TrimEntries)[0];

    private static string Failure(string sentence, ProcessOutcome outcome)
    {
        var line = FirstLine(outcome.StandardError);
        return line.Length == 0 ? sentence + "." : sentence + ": " + line;
    }

    private async Task<MentionContent> ReadWorkingAsync(int maxBytes, CancellationToken cancellationToken)
    {
        var tracked = await RunCheckedAsync(["diff", .. DiffOptions, "HEAD", "--", "."], maxBytes, "Git could not produce the diff", cancellationToken).ConfigureAwait(false);
        var untracked = await ListUntrackedAsync(cancellationToken).ConfigureAwait(false);
        if (tracked.StandardOutput.Length == 0 && untracked.Length == 0)
        {
            return NoChanges("There are no uncommitted changes.");
        }

        var text = new StringBuilder(tracked.StandardOutput);
        var used = Encoding.UTF8.GetByteCount(tracked.StandardOutput);
        var total = (long)used;
        var truncated = tracked.StandardOutputTruncated || untracked.Length > MaxUntrackedFiles;
        foreach (var path in untracked.Take(MaxUntrackedFiles))
        {
            if (used >= maxBytes)
            {
                truncated = true;
                break;
            }

            var piece = await RunAsync(["diff", "--no-index", .. DiffOptions, "--", "/dev/null", path], maxBytes - used, cancellationToken).ConfigureAwait(false);
            if (piece.ExitCode is not (0 or 1))
            {
                throw new MentionUnavailableException(Failure("Git could not show the new file '" + path + "'", piece));
            }

            var size = Encoding.UTF8.GetByteCount(piece.StandardOutput);
            text.Append(piece.StandardOutput);
            used += size;
            total += size;
            truncated |= piece.StandardOutputTruncated;
        }

        var cut = MentionTextCut.Cut(text.ToString(), maxBytes);
        return new MentionContent(cut.Text, Math.Max(total, cut.SizeBytes), truncated || cut.Truncated);
    }

    private async Task EnsureCommitAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_root))
        {
            throw new MentionUnavailableException(MentionCopy.NoRepository);
        }

        var inside = await RunAsync(["rev-parse", "--is-inside-work-tree"], ShortCharacters, cancellationToken).ConfigureAwait(false);
        if (inside.ExitCode != 0 || !string.Equals(inside.StandardOutput.Trim(), "true", StringComparison.Ordinal))
        {
            throw new MentionUnavailableException(MentionCopy.NoRepository);
        }

        var head = await RunAsync(["rev-parse", "--verify", "--quiet", "HEAD^{commit}"], ShortCharacters, cancellationToken).ConfigureAwait(false);
        if (head.ExitCode != 0)
        {
            throw new MentionUnavailableException("This repository has no commits yet.");
        }
    }

    private async Task<string> ResolveBaseAsync(CancellationToken cancellationToken)
    {
        var originHead = await RunAsync(["symbolic-ref", "--quiet", "--short", "refs/remotes/origin/HEAD"], ShortCharacters, cancellationToken).ConfigureAwait(false);
        var candidates = new List<string>(BaseCandidates.Length + 1);
        if (originHead.ExitCode == 0 && originHead.StandardOutput.Trim() is { Length: > 0 } fromOrigin)
        {
            candidates.Add(fromOrigin);
        }

        candidates.AddRange(BaseCandidates);
        foreach (var candidate in candidates.Distinct(StringComparer.Ordinal))
        {
            var verified = await RunAsync(["rev-parse", "--verify", "--quiet", candidate + "^{commit}"], ShortCharacters, cancellationToken).ConfigureAwait(false);
            if (verified.ExitCode == 0)
            {
                return candidate;
            }
        }

        throw new MentionUnavailableException("Git found no base branch to compare with. It looked for origin/HEAD, main and master.");
    }

    private async Task<string> MergeBaseAsync(string baseRef, CancellationToken cancellationToken)
    {
        var outcome = await RunAsync(["merge-base", baseRef, "HEAD"], ShortCharacters, cancellationToken).ConfigureAwait(false);
        var commit = outcome.StandardOutput.Trim();
        if (outcome.ExitCode != 0 || commit.Length == 0)
        {
            throw new MentionUnavailableException("This branch has no history in common with " + baseRef + ".");
        }

        return commit;
    }

    private async Task<int> CountNumstatAsync(IReadOnlyList<string> revisions, CancellationToken cancellationToken)
    {
        var numstat = await RunCheckedAsync(["diff", "--numstat", .. DiffOptions, .. revisions, "--", "."], ListingCharacters, "Git could not count the changes", cancellationToken).ConfigureAwait(false);
        return numstat.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    private async Task<string[]> ListUntrackedAsync(CancellationToken cancellationToken)
    {
        var listing = await RunCheckedAsync(["ls-files", "--others", "--exclude-standard", "-z", "--", "."], ListingCharacters, "Git could not list the new files", cancellationToken).ConfigureAwait(false);
        return listing.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private async Task<ProcessOutcome> RunCheckedAsync(IReadOnlyList<string> arguments, int maxCharacters, string failure, CancellationToken cancellationToken)
    {
        var outcome = await RunAsync(arguments, maxCharacters, cancellationToken).ConfigureAwait(false);
        return outcome.ExitCode == 0
            ? outcome
            : throw new MentionUnavailableException(Failure(failure, outcome));
    }

    private async Task<ProcessOutcome> RunAsync(IReadOnlyList<string> arguments, int maxCharacters, CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(_timeout, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            return await _runner.RunAsync(new ProcessRequest(_git, [.. GlobalOptions, .. arguments], _root, Math.Max(0, maxCharacters)), linked.Token).ConfigureAwait(false);
        }
        catch (Win32Exception exception)
        {
            throw new MentionUnavailableException("Git is not installed or is not on the PATH.", exception);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new MentionUnavailableException(string.Create(CultureInfo.InvariantCulture, $"Git took longer than {_timeout.TotalSeconds:0} seconds to answer."));
        }
    }
}
