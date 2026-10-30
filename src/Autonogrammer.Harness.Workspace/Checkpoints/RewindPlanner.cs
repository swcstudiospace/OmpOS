namespace Autonogrammer.Workspace.Checkpoints;

/// <summary>Works out what rewinding to before a turn does to each file. It reads files and stored copies and writes nothing.</summary>
internal static class RewindPlanner
{
    /// <summary>Plans a rewind.</summary>
    /// <param name="document">The session's timeline.</param>
    /// <param name="turnId">The turn the files go back to the state before.</param>
    /// <param name="roots">The open workspace folders.</param>
    /// <param name="blobs">The stored copies.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The public plan and, for each file that may be written, the data to write it.</returns>
    public static async Task<(RewindPlan Plan, IReadOnlyList<PlannedWrite> Writes)> PlanAsync(
        CheckpointDocument document,
        int turnId,
        CheckpointRoots roots,
        CheckpointBlobs blobs,
        CancellationToken cancellationToken)
    {
        var order = new List<string>();
        var first = new Dictionary<string, CheckpointFileDocument>(StringComparer.Ordinal);
        var last = new Dictionary<string, CheckpointFileDocument>(StringComparer.Ordinal);
        foreach (var turn in document.Turns.Where(turn => turn.TurnId >= turnId).OrderBy(static turn => turn.TurnId))
        {
            foreach (var file in turn.Files)
            {
                var key = file.Root + "\0" + file.Path;
                if (first.TryAdd(key, file))
                {
                    order.Add(key);
                }

                last[key] = file;
            }
        }

        var plans = new List<RewindFilePlan>(order.Count);
        var writes = new List<PlannedWrite>();
        foreach (var key in order)
        {
            var (plan, write) = await PlanFileAsync(first[key], last[key], roots, blobs, cancellationToken).ConfigureAwait(false);
            plans.Add(plan);
            if (write is not null)
            {
                writes.Add(write);
            }
        }

        return (new RewindPlan(document.SessionId, turnId, plans), writes);
    }

    private static async Task<(RewindFilePlan Plan, PlannedWrite? Write)> PlanFileAsync(
        CheckpointFileDocument earliest,
        CheckpointFileDocument latest,
        CheckpointRoots roots,
        CheckpointBlobs blobs,
        CancellationToken cancellationToken)
    {
        var deletes = earliest.BeforeHash is null;
        var absolute = roots.Resolve(earliest.Root, earliest.Path, out var reason);
        if (absolute is null)
        {
            return (Plan(earliest, RewindFileAction.Refused, deletes, earliest.Path + ": " + reason), null);
        }

        if (Directory.Exists(absolute))
        {
            return (Plan(earliest, RewindFileAction.Refused, deletes, earliest.Path + " is a folder now, so it is left alone."), null);
        }

        var (current, readFailure) = await ReadCurrentAsync(absolute, cancellationToken).ConfigureAwait(false);
        if (readFailure is not null)
        {
            return (Plan(earliest, RewindFileAction.Refused, deletes, earliest.Path + " could not be read: " + readFailure), null);
        }

        var currentHash = current is null ? null : CheckpointHash.Of(current);
        if (string.Equals(currentHash, earliest.BeforeHash, StringComparison.Ordinal))
        {
            return (Plan(earliest, RewindFileAction.AlreadyAtTarget, deletes, earliest.Path + " already holds its content from before the turn."), null);
        }

        byte[]? content = null;
        if (!deletes)
        {
            content = earliest.BeforeBlob is null ? null : await blobs.ReadAsync(earliest.BeforeBlob, cancellationToken).ConfigureAwait(false);
            if (content is null)
            {
                var why = earliest.Note.Length > 0 ? earliest.Note : "the stored copy is missing or damaged";
                return (Plan(earliest, RewindFileAction.Unrestorable, deletes, earliest.Path + ": its earlier content cannot be restored (" + why.TrimEnd('.') + ")."), null);
            }

            content = LineEndings.Adapt(content, current ?? []);
        }

        var stale = !string.Equals(currentHash, latest.AfterHash, StringComparison.Ordinal);
        var (action, detail) = (stale, deletes) switch
        {
            (true, _) => (RewindFileAction.Stale, earliest.Path + " changed after the agent's edit, so it is left alone unless you force the rewind."),
            (false, true) => (RewindFileAction.Delete, "Deletes " + earliest.Path + ", which the agent created."),
            _ => (RewindFileAction.Restore, "Restores " + earliest.Path + " to its content from before the turn."),
        };
        var plan = Plan(earliest, action, deletes, detail);
        return (plan, new PlannedWrite(plan, absolute, content, currentHash));
    }

    private static async Task<(byte[]? Content, string? Failure)> ReadCurrentAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return (File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false) : null, null);
        }
        catch (IOException exception)
        {
            return (null, exception.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return (null, "access is denied.");
        }
    }

    private static RewindFilePlan Plan(CheckpointFileDocument file, RewindFileAction action, bool deletes, string detail) =>
        new(file.Root, file.Path, action, deletes, detail);
}
