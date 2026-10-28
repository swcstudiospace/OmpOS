using OmpOS.Presentation.Worktrees;

namespace OmpOS.Presentation.Tests.Worktrees;

/// <summary>A best-of-n host that finishes each candidate as scripted and records the requests.</summary>
internal sealed class ScriptedBestOfNHost : IBestOfNHost
{
    /// <summary>Gets the requests it received.</summary>
    public List<BestOfNRunRequest> Requests { get; } = [];

    /// <summary>Gets or sets the candidate numbers that fail.</summary>
    public HashSet<int> Failing { get; set; } = [];

    /// <inheritdoc/>
    public Task<BestOfNRunResult> RunAsync(BestOfNRunRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(Failing.Contains(request.Index) ? new BestOfNRunResult(false, "ran out of context") : new BestOfNRunResult(true, "done " + request.Index));
    }
}
