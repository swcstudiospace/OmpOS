using Autonogrammer.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Plugins.Tests.Hooks;

/// <summary>Legacy hook files are listed after the current file and before Cursor, and legacy managed paths stay read-only candidates.</summary>
public sealed class HookSourceLegacyTests
{
    private const string Json = """{ "version": 1, "hooks": {} }""";

    /// <summary>Project and user legacy hooks sit between the current file and Cursor. The Linux managed legacy path is below the current managed path.</summary>
    [Fact]
    public void LegacyHooksSitBelowTheCurrentFile()
    {
        var root = Path.Combine("/work", "legacy-hooks");
        var home = Path.Combine("/home", "legacy-hooks");
        var files = new MemoryHookFileSource()
            .With(Path.Combine(root, ".autonogrammer", "hooks.json"), Json)
            .With(Path.Combine(root, LegacyBrandNames.DirectoryName, "hooks.json"), Json)
            .With(Path.Combine(root, ".cursor", "hooks.json"), Json)
            .With(Path.Combine(home, ".autonogrammer", "hooks.json"), Json)
            .With(Path.Combine(home, LegacyBrandNames.DirectoryName, "hooks.json"), Json)
            .With(Path.Combine(home, ".cursor", "hooks.json"), Json)
            .With(HookSourceLocator.ManagedPath(HookPlatform.Linux, string.Empty), Json)
            .With(HookSourceLocator.LegacyManagedPath(HookPlatform.Linux, string.Empty), Json);

        var sources = HookSourceLocator.Locate(new HookLocatorContext([root], home, HookPlatform.Linux, IncludeThirdPartyConfigs: false), files);
        var paths = sources.Select(source => source.Path).ToArray();
        Index(paths, HookSourceLocator.ManagedPath(HookPlatform.Linux, string.Empty)).ShouldBeLessThan(Index(paths, HookSourceLocator.LegacyManagedPath(HookPlatform.Linux, string.Empty)));
        Index(paths, Path.Combine(root, ".autonogrammer", "hooks.json")).ShouldBeLessThan(Index(paths, Path.Combine(root, LegacyBrandNames.DirectoryName, "hooks.json")));
        Index(paths, Path.Combine(root, LegacyBrandNames.DirectoryName, "hooks.json")).ShouldBeLessThan(Index(paths, Path.Combine(root, ".cursor", "hooks.json")));
        Index(paths, Path.Combine(home, LegacyBrandNames.DirectoryName, "hooks.json")).ShouldBeLessThan(Index(paths, Path.Combine(home, ".cursor", "hooks.json")));
    }

    /// <summary>Windows and macOS legacy managed paths compile and name the legacy directory. They are not opened.</summary>
    [Fact]
    public void OtherPlatformManagedPathsAreCompileChecked()
    {
        HookSourceLocator.LegacyManagedPath(HookPlatform.Linux, string.Empty).ShouldBe(Path.Combine("/etc", LegacyBrandNames.Lower, "hooks.json"));
        HookSourceLocator.LegacyManagedPath(HookPlatform.MacOs, string.Empty).ShouldContain(LegacyBrandNames.Pascal);
        HookSourceLocator.LegacyManagedPath(HookPlatform.Windows, Path.Combine("D:", "Data")).ShouldContain(LegacyBrandNames.Pascal);
    }

    private static int Index(string[] paths, string path) => Array.FindIndex(paths, item => string.Equals(item, path, StringComparison.Ordinal));
}
