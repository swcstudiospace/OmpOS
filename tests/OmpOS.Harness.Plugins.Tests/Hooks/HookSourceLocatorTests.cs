using OmpOS.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Verifies which hook files are found, in which order, with which working directory (design 2.1).
/// </summary>
public sealed class HookSourceLocatorTests
{
    private const string Json = """{ "version": 1, "hooks": {} }""";
    private const string Claude = """{ "hooks": {} }""";

    private static readonly string Home = Path.Combine("/home", "ada");
    private static readonly string Root = Path.Combine("/work", "app");

    private static MemoryHookFileSource EveryFile()
    {
        var files = new MemoryHookFileSource();
        foreach (var path in new[]
        {
            HookSourceLocator.ManagedPath(HookPlatform.Linux, string.Empty),
            Path.Combine(Root, ".ompos", "hooks.json"),
            Path.Combine(Root, ".cursor", "hooks.json"),
            Path.Combine(Home, ".ompos", "hooks.json"),
            Path.Combine(Home, ".cursor", "hooks.json"),
        })
        {
            files.With(path, Json);
        }

        foreach (var path in new[]
        {
            Path.Combine(Root, ".claude", "settings.local.json"),
            Path.Combine(Root, ".claude", "settings.json"),
            Path.Combine(Home, ".claude", "settings.json"),
        })
        {
            files.With(path, Claude);
        }

        return files;
    }

    private static HookLocatorContext Context(bool remote = false, bool thirdParty = true, params string[] roots) =>
        new(roots.Length == 0 ? [Root] : roots, Home, HookPlatform.Linux, remote, thirdParty);

    [Fact]
    public void SourcesAreReturnedInPriorityOrderWithSequentialPriorities()
    {
        var sources = HookSourceLocator.Locate(Context(), EveryFile());

        sources.Select(source => source.Scope).ShouldBe(
            [
                HookScope.Managed,
                HookScope.Project,
                HookScope.Project,
                HookScope.User,
                HookScope.User,
                HookScope.ClaudeLocal,
                HookScope.ClaudeProject,
                HookScope.ClaudeUser,
            ],
            ignoreOrder: false);
        sources.Select(source => source.Priority).ShouldBe([0, 1, 2, 3, 4, 5, 6, 7], ignoreOrder: false);
        sources[1].Path.ShouldBe(Path.Combine(Root, ".ompos", "hooks.json"), Case.Sensitive);
        sources[2].Path.ShouldBe(Path.Combine(Root, ".cursor", "hooks.json"), Case.Sensitive);
    }

    [Fact]
    public void ProjectHooksRunInTheWorkspaceRootAndUserHooksInTheirOwnDirectory()
    {
        var sources = HookSourceLocator.Locate(Context(), EveryFile());

        sources.Single(source => source.Path == Path.Combine(Root, ".cursor", "hooks.json")).WorkingDirectory.ShouldBe(Root, Case.Sensitive);
        sources.Single(source => source.Path == Path.Combine(Home, ".cursor", "hooks.json")).WorkingDirectory.ShouldBe(Path.Combine(Home, ".cursor"), Case.Sensitive);
        sources.Single(source => source.Scope == HookScope.Managed).WorkingDirectory.ShouldBe(Path.Combine("/etc", "ompos"), Case.Sensitive);
        sources.Single(source => source.Scope == HookScope.ClaudeUser).WorkingDirectory.ShouldBe(Path.Combine(Home, ".claude"), Case.Sensitive);
        sources.Single(source => source.Scope == HookScope.ClaudeProject).WorkingDirectory.ShouldBe(Root, Case.Sensitive);
    }

    [Fact]
    public void RemoteSessionsDropUserScopesButKeepManagedAndProject()
    {
        var sources = HookSourceLocator.Locate(Context(remote: true), EveryFile());

        sources.Select(source => source.Scope).ShouldNotContain(HookScope.User);
        sources.Select(source => source.Scope).ShouldNotContain(HookScope.ClaudeUser);
        sources.Select(source => source.Scope).ShouldContain(HookScope.Managed);
        sources.Select(source => source.Scope).ShouldContain(HookScope.Project);
    }

    [Fact]
    public void ThirdPartyDiscoveryCanBeSwitchedOff()
    {
        var sources = HookSourceLocator.Locate(Context(thirdParty: false), EveryFile());

        sources.Select(source => source.Format).ShouldAllBe(format => format == HookConfigFormat.HooksJson);
        sources.Count.ShouldBe(5);
    }

    [Fact]
    public void OnlyFilesThatExistAreReturned()
    {
        var files = new MemoryHookFileSource().With(Path.Combine(Root, ".cursor", "hooks.json"), Json);

        var sources = HookSourceLocator.Locate(Context(), files);

        sources.Single().Path.ShouldBe(Path.Combine(Root, ".cursor", "hooks.json"), Case.Sensitive);
    }

    [Fact]
    public void EachRootOfAMultiRootWorkspaceHasItsOwnProjectFileAndPseudoPluginId()
    {
        var second = Path.Combine("/work", "lib");
        var files = new MemoryHookFileSource()
            .With(Path.Combine(Root, ".cursor", "hooks.json"), Json)
            .With(Path.Combine(second, ".cursor", "hooks.json"), Json);

        var sources = HookSourceLocator.Locate(Context(roots: [Root, second]), files);

        sources.Count.ShouldBe(2);
        sources[0].WorkingDirectory.ShouldBe(Root, Case.Sensitive);
        sources[1].WorkingDirectory.ShouldBe(second, Case.Sensitive);
        sources[0].PseudoPluginId.ShouldStartWith("cursor-project:", Case.Sensitive);
        sources[0].PseudoPluginId.ShouldNotBe(sources[1].PseudoPluginId, Case.Sensitive);
        sources[0].PseudoPluginId.ShouldBe("cursor-project:" + HookSourceLocator.RootHash(Root), Case.Sensitive);
    }

    [Fact]
    public void RootHashIsTwelveStableLowercaseHexCharacters()
    {
        var hash = HookSourceLocator.RootHash(Root);

        hash.Length.ShouldBe(12);
        hash.ShouldBe(HookSourceLocator.RootHash(Root), Case.Sensitive);
        hash.ShouldMatch("^[0-9a-f]{12}$");
        hash.ShouldNotBe(HookSourceLocator.RootHash(Root + "2"), Case.Sensitive);
    }

    [Fact]
    public void ManagedPathsAreOmpOsOwnPerPlatformAndNeverCursorsEnterprisePaths()
    {
        HookSourceLocator.ManagedPath(HookPlatform.Linux, string.Empty).ShouldBe(Path.Combine("/etc", "ompos", "hooks.json"), Case.Sensitive);
        HookSourceLocator.ManagedPath(HookPlatform.MacOs, string.Empty).ShouldBe(Path.Combine("/Library", "Application Support", "OmpOS", "hooks.json"), Case.Sensitive);
        HookSourceLocator.ManagedPath(HookPlatform.Windows, @"D:\Data").ShouldBe(Path.Combine(@"D:\Data", "OmpOS", "hooks.json"), Case.Sensitive);
        foreach (var platform in Enum.GetValues<HookPlatform>())
        {
            HookSourceLocator.ManagedPath(platform, @"C:\ProgramData").ShouldNotContain("ursor", Case.Insensitive);
        }
    }

    [Fact]
    public void ManagedFileIsReadFromThePlatformPathOfTheContext()
    {
        var files = new MemoryHookFileSource().With(HookSourceLocator.ManagedPath(HookPlatform.MacOs, string.Empty), Json);

        var linux = HookSourceLocator.Locate(new HookLocatorContext([Root], Home, HookPlatform.Linux), files);
        var mac = HookSourceLocator.Locate(new HookLocatorContext([Root], Home, HookPlatform.MacOs), files);

        linux.ShouldBeEmpty();
        mac.Single().Scope.ShouldBe(HookScope.Managed);
    }

    [Fact]
    public void WithoutAHomeDirectoryUserScopesAreSkipped()
    {
        var sources = HookSourceLocator.Locate(new HookLocatorContext([Root], null, HookPlatform.Linux), EveryFile());

        sources.Select(source => source.Scope).ShouldNotContain(HookScope.User);
        sources.Select(source => source.Scope).ShouldNotContain(HookScope.ClaudeUser);
    }
}
