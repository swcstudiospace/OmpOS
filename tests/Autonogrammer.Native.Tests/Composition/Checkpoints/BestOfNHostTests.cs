using Autonogrammer.Native.Composition.Checkpoints;
using Autonogrammer.Presentation.Worktrees;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Composition.Checkpoints;

/// <summary>The best-of-n adapter: the candidate's final text is its summary, and a fault is a failed candidate, not an exception.</summary>
public sealed class BestOfNHostTests
{
    private static BestOfNRunRequest Request() =>
        new("fix the bug", 1, 3, new WorktreeRunInfo("run-1", "/tmp/run-1", "autonogrammer/run/run-1", 0, null));

    /// <summary>The delegate gets the request and its text becomes the summary.</summary>
    [Fact]
    public async Task ASuccessfulCandidateReportsItsFinalText()
    {
        BestOfNRunRequest? seen = null;
        var host = new BestOfNHost((request, _) =>
        {
            seen = request;
            return Task.FromResult("done: fixed");
        });

        var result = await host.RunAsync(Request(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Succeeded.ShouldBeTrue();
        result.Summary.ShouldBe("done: fixed");
        seen.ShouldNotBeNull().Run.Path.ShouldBe("/tmp/run-1");
    }

    /// <summary>A candidate that throws is reported with its message.</summary>
    [Fact]
    public async Task AFaultingCandidateIsAFailedResult()
    {
        var host = new BestOfNHost(static (_, _) => throw new InvalidOperationException("engine crashed"));

        var result = await host.RunAsync(Request(), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        result.Summary.ShouldBe("engine crashed");
    }

    /// <summary>A cancellation requested by the caller is not a candidate fault.</summary>
    [Fact]
    public async Task ACancellationByTheCallerPropagates()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync().ConfigureAwait(true);
        var host = new BestOfNHost(static (_, token) => Task.FromCanceled<string>(token));

        await Should.ThrowAsync<OperationCanceledException>(() => host.RunAsync(Request(), source.Token)).ConfigureAwait(true);
    }
}
