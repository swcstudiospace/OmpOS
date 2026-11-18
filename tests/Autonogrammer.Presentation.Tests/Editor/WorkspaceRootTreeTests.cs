using Autonogrammer.Presentation.Editor;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests;

/// <summary>The multi-root explorer's roots: a root that fails to open with any error shows that error on its own row, and the other roots still open.</summary>
public sealed class WorkspaceRootTreeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A root whose open fails with an error that is not an I/O error is shown as that root's failure, and the other root opens.</summary>
    [Fact]
    public async Task AnUnexpectedFailureOpeningOneRootIsShownOnlyOnThatRoot()
    {
        var web = new InMemoryWorkspaceFileSystem("/repos/web").WithFile("package.json");
        var explorer = new MultiRootWorkspaceTree();

        await explorer.OpenAsync(
        [
            new AsyncWorkspaceRootSource("web", "web", true, _ => Task.FromResult<IAsyncWorkspaceFileSystem>(new YieldingAsyncWorkspaceFileSystem(web))),
            new AsyncWorkspaceRootSource("remote", "remote", false, _ => Task.FromException<IAsyncWorkspaceFileSystem>(new TimeoutException("ssh did not answer"))),
        ], Token).ConfigureAwait(true);

        explorer.Roots[0].HasError.ShouldBeFalse();
        explorer.Roots[0].Tree.Nodes.Select(node => node.Name).ShouldBe(["package.json"]);
        explorer.Roots[1].HasError.ShouldBeTrue();
        explorer.Roots[1].Error.ShouldBe("remote: ssh did not answer");
    }
}
