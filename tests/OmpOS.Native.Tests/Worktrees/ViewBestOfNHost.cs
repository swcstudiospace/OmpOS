using OmpOS.Presentation.Worktrees;

namespace OmpOS.Native.Tests.Worktrees;

/// <summary>A best-of-n host that finishes every candidate and records the requests, for the view tests.</summary>
internal sealed class ViewBestOfNHost : IBestOfNHost
{
    /// <summary>Gets the requests it received.</summary>
    public List<BestOfNRunRequest> Requests { get; } = [];

    /// <inheritdoc/>
    public Task<BestOfNRunResult> RunAsync(BestOfNRunRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(new BestOfNRunResult(true, "done " + request.Index));
    }
}
