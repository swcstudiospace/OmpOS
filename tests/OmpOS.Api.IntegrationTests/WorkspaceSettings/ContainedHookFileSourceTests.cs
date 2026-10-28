using OmpOS.Api.Services.WorkspaceSettings;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.WorkspaceSettings;

/// <summary>The hook file access of the API: inside the allowed folders only, along paths that stay inside after every link is resolved.</summary>
public sealed class ContainedHookFileSourceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A file inside the workspace folder is inspected, read and digested.</summary>
    [Fact]
    public async Task AFileInsideTheWorkspace_IsRead()
    {
        using var workspace = new TempWorkspace();
        var path = workspace.Write(".cursor/hooks.json", "{}");
        var source = new ContainedHookFileSource(workspace.Root, null);

        source.Inspect(path).Exists.ShouldBeTrue();
        (await source.ReadAsync(path, 1024, Token).ConfigureAwait(true)).ShouldNotBeNull().Length.ShouldBe(2);
        (await source.DigestAsync(path, 1024, Token).ConfigureAwait(true)).ShouldNotBeNull().Length.ShouldBe(64);
    }

    /// <summary>A file outside every allowed folder, such as the machine-wide managed file, is reported as absent.</summary>
    [Fact]
    public async Task AFileOutsideTheAllowedFolders_IsAbsent()
    {
        using var workspace = new TempWorkspace();
        var outside = workspace.NewFolder();
        var path = workspace.Write("hooks.json", "{}", outside);
        var source = new ContainedHookFileSource(workspace.Root, null);

        source.Inspect(path).Exists.ShouldBeFalse();
        (await source.ReadAsync(path, 1024, Token).ConfigureAwait(true)).ShouldBeNull();
        (await source.DigestAsync(path, 1024, Token).ConfigureAwait(true)).ShouldBeNull();
    }

    /// <summary>A link inside the workspace that points outside is reported as absent.</summary>
    [Fact]
    public async Task ALinkThatLeavesTheWorkspace_IsAbsent()
    {
        using var workspace = new TempWorkspace();
        var outside = workspace.NewFolder();
        var target = workspace.Write("hooks.json", "{}", outside);
        if (!workspace.TryLink("hooks.json", target))
        {
            Assert.Skip("This account cannot create symbolic links.");
        }

        var source = new ContainedHookFileSource(workspace.Root, null);
        var linked = Path.Combine(workspace.Root, "hooks.json");

        source.Inspect(linked).Exists.ShouldBeFalse();
        (await source.ReadAsync(linked, 1024, Token).ConfigureAwait(true)).ShouldBeNull();
    }

    /// <summary>The home directory is readable only when it is passed in.</summary>
    [Fact]
    public void TheHome_IsReadableOnlyWhenPassedIn()
    {
        using var workspace = new TempWorkspace();
        var home = workspace.NewFolder();
        var path = workspace.Write(".cursor/hooks.json", "{}", home);

        new ContainedHookFileSource(workspace.Root, null).Inspect(path).Exists.ShouldBeFalse();
        new ContainedHookFileSource(workspace.Root, home).Inspect(path).Exists.ShouldBeTrue();
    }
}
