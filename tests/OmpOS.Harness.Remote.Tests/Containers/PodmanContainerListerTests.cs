using System.Text.Json;
using OmpOS.Contracts.Environments;
using OmpOS.Harness.Remote.Containers;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>The container listing behind the "add from a running container" picker: <c>podman ps --format json</c> read into identities, and its failures.</summary>
public sealed class PodmanContainerListerTests
{
    private const string Listing = """
        [
          {"Id":"aaa111","Names":["devbox"],"Image":"registry.example/dev:1","State":"running","Labels":{"devcontainer.local_folder":"/home/me/proj","x":"y"},"Command":["sleep","1"],"Env":["OMPOS_SECRET=hunter2"]},
          {"Id":"bbb222","Names":["db","alias"],"Image":"registry.example/pg:16","State":"exited","Labels":null},
          {"Id":"ccc333","Names":["-bad"],"Image":"x","State":"running"},
          {"Names":["noid"],"Image":"x"},
          "junk",
          {"Id":"ddd444","Names":[],"Image":"x"},
          {"Id":"eee555","Names":"single","Image":"registry.example/s:1","State":"running","Labels":{}}
        ]
        """;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The identity of every valid container is kept (id, first name, image, state, dev container folder); invalid entries and everything else are dropped.</summary>
    [Fact]
    public void OnlyIdentitiesAreKept()
    {
        var containers = PodmanContainerListParser.Parse(Listing);

        containers.Select(container => container.Name).ShouldBe(["devbox", "db", "single"]);
        containers[0].ShouldBe(new ContainerSummary("aaa111", "devbox", "registry.example/dev:1", "running", "/home/me/proj"));
        containers[1].DevContainerFolder.ShouldBeNull();
        containers[1].IsRunning.ShouldBeFalse();
        containers[2].State.ShouldBe("running", Case.Sensitive);
        string.Join(' ', containers.Select(container => container.ToString())).ShouldNotContain("hunter2", Case.Sensitive);
    }

    /// <summary>Output that is not a JSON array is a JSON error.</summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("{\"Id\":\"a\"}")]
    [InlineData("")]
    public void NonArrayOutputIsAnError(string text)
    {
        Should.Throw<JsonException>(() => PodmanContainerListParser.Parse(text));
    }

    /// <summary>The list is capped so a huge answer cannot flood the picker.</summary>
    [Fact]
    public void TheListIsCapped()
    {
        var entries = Enumerable.Range(0, 800).Select(index => "{\"Id\":\"id" + index + "\",\"Names\":[\"c" + index + "\"],\"Image\":\"i\",\"State\":\"running\"}");

        PodmanContainerListParser.Parse("[" + string.Join(',', entries) + "]").Count.ShouldBe(500);
    }

    /// <summary>The lister runs <c>podman ps --format json</c> and returns what podman printed.</summary>
    [Fact]
    public async Task TheListerRunsPsAndReturnsTheContainers()
    {
        using var host = FakeContainerHost.Create();
        host.SetPsOutput(Listing);

        var listing = await new PodmanContainerLister(host.ClientOptions).ListRunningAsync(Token).ConfigureAwait(true);

        listing.Problem.ShouldBeNull();
        listing.Containers.Select(container => container.Name).ShouldBe(["devbox", "db", "single"]);
        host.PodmanCalls().ShouldHaveSingleItem().ToArray().ShouldBe(["ps", "--format", "json"]);
    }

    /// <summary>A missing podman, a down service and a non-JSON answer are problems with a diagnosis, never exceptions.</summary>
    [Fact]
    public async Task FailuresAreProblemsWithADiagnosis()
    {
        var missing = await new PodmanContainerLister(new PodmanClientOptions { PodmanPath = "/nonexistent/ompos-test-podman" }).ListRunningAsync(Token).ConfigureAwait(true);
        using var down = FakeContainerHost.Create(new FakeContainerHostOptions { Mode = "service-down" });
        var unavailable = await new PodmanContainerLister(down.ClientOptions).ListRunningAsync(Token).ConfigureAwait(true);
        using var odd = FakeContainerHost.Create();
        odd.SetPsOutput("this is not json");
        var garbage = await new PodmanContainerLister(odd.ClientOptions).ListRunningAsync(Token).ConfigureAwait(true);

        missing.Containers.ShouldBeEmpty();
        missing.Problem.ShouldNotBeNull().Kind.ShouldBe(RemoteDiagnosisKind.ContainerRuntimeMissing);
        unavailable.Problem.ShouldNotBeNull().Kind.ShouldBe(RemoteDiagnosisKind.ContainerRuntimeUnavailable);
        garbage.Problem.ShouldNotBeNull().Kind.ShouldBe(RemoteDiagnosisKind.Unknown);
        garbage.Containers.ShouldBeEmpty();
    }

    /// <summary>The folder search passes the label filter and returns the labelled containers.</summary>
    [Fact]
    public async Task TheFolderSearchUsesTheLabelFilter()
    {
        using var host = FakeContainerHost.Create();
        host.SetFilteredPsOutput(Listing);
        var folder = Path.Combine(Path.GetTempPath(), "proj with space");

        var listing = await new PodmanContainerLister(host.ClientOptions).FindForFolderAsync(folder, Token).ConfigureAwait(true);

        listing.Containers.Count.ShouldBe(3);
        host.PodmanCalls().ShouldHaveSingleItem().ShouldContain("label=devcontainer.local_folder=" + folder, StringComparer.Ordinal);
    }

    /// <summary>The wsl listing parser reads UTF-16 text decoded as UTF-8: NUL characters, a byte-order mark, replacement characters and blank lines are removed.</summary>
    [Fact]
    public void TheWslListIsCleaned()
    {
        var text = "\uFEFFU\0b\0u\0n\0t\0u\0\r\0\n\0\r\n\0D\0e\0b\0i\0a\0n\0\r\0\n\0";

        WslDistributionParser.Parse(text).ShouldBe(["Ubuntu", "Debian"]);
        WslDistributionParser.Parse(string.Empty).ShouldBeEmpty();
        WslDistributionParser.Parse("Ubuntu\r\n  \r\nDebian\r\n").ShouldBe(["Ubuntu", "Debian"]);
    }
}
