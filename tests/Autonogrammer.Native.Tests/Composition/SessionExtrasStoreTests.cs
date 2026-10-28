using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Tests.AgentLoop;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Composition;

/// <summary>The sidecar file that remembers the ssh environment and the extra roots of a session.</summary>
public sealed class SessionExtrasStoreTests
{
    [Fact]
    public void AnUnknownSessionIsLocalAndSingleRoot()
    {
        using var tree = new TempTree();

        var extras = new SessionExtrasStore(tree.Root).Get("s1");

        extras.IsRemote.ShouldBeFalse();
        extras.AdditionalDirectories.ShouldBeEmpty();
    }

    [Fact]
    public void WhatIsSetSurvivesANewStoreOverTheSameFolder()
    {
        using var tree = new TempTree();
        new SessionExtrasStore(tree.Root).Set("s1", new SessionExtras("env-1", ["/a", "/b"]));

        var extras = new SessionExtrasStore(tree.Root).Get("s1");

        extras.EnvironmentId.ShouldBe("env-1");
        extras.AdditionalDirectories.ShouldBe(["/a", "/b"]);
    }

    [Fact]
    public void ALocalSingleRootSessionRemovesItsEntry()
    {
        using var tree = new TempTree();
        var store = new SessionExtrasStore(tree.Root);
        store.Set("s1", new SessionExtras("env-1", []));

        store.Set("s1", SessionExtras.Local);

        new SessionExtrasStore(tree.Root).Get("s1").IsRemote.ShouldBeFalse();
    }

    [Fact]
    public void ACorruptFileReadsAsEmptyAndIsReplacedByTheNextWrite()
    {
        using var tree = new TempTree();
        File.WriteAllText(Path.Combine(tree.Root, SessionExtrasStore.FileName), "{ not json");
        var store = new SessionExtrasStore(tree.Root);

        store.Get("s1").IsRemote.ShouldBeFalse();
        store.Set("s1", new SessionExtras("env-2", []));

        new SessionExtrasStore(tree.Root).Get("s1").EnvironmentId.ShouldBe("env-2");
    }
}
