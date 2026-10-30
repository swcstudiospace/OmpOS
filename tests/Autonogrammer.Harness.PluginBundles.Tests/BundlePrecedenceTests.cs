using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.PluginBundles.Tests.Support;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.PluginBundles.Tests;

/// <summary>A bundle skill or command that reuses a built-in id loses. The built-in is not shadowed.</summary>
public sealed class BundlePrecedenceTests
{
    /// <summary>Install refuses the collision, and an already-installed collision is reported while the built-in wins.</summary>
    [Fact]
    public void BuiltinWinsAndCollisionIsReported()
    {
        var root = Path.Combine(Path.GetTempPath(), "ag-bundle-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "src");
        var destination = Path.Combine(root, "plugins");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(Path.Combine(source, "skills", "create-skill"));
        File.WriteAllText(Path.Combine(source, "skills", "create-skill", "SKILL.md"), "# no\n");
        Directory.CreateDirectory(Path.Combine(source, "commands"));
        File.WriteAllText(Path.Combine(source, "commands", "review.md"), "# no\n");
        try
        {
            var installer = new BundleInstaller(
                () => new HashSet<string>(["create-skill"], StringComparer.Ordinal),
                () => new HashSet<string>(["review"], StringComparer.Ordinal));
            var error = Should.Throw<BundleValidationException>(() => installer.Install(source, destination, BundleInstallScope.User));
            error.Message.ShouldBe("BundleInvalid(BundleNameCollision)");
            Directory.Exists(Path.Combine(destination, "src")).ShouldBeFalse();

            var installed = Path.Combine(destination, "p");
            BundleFixtureBuilder.WriteFolder(installed, "create-skill");
            Directory.CreateDirectory(Path.Combine(installed, "commands"));
            File.WriteAllText(Path.Combine(installed, "commands", "review.md"), "# no\n");
            var skills = new SkillStateStore(Path.Combine(root, "skills-state.json"));
            var bundles = new BundleStateStore(Path.Combine(root, "plugins-state.json"));
            var contributor = new BundleLaunchContributor([destination], skills, bundles, new HashSet<string>(["create-skill"], StringComparer.Ordinal));
            var contribution = contributor.Contribute(
                new LaunchRequest { Surface = "desktop", SessionId = "s", WorkingDirectory = "/work", TrustedFolders = [] },
                CancellationToken.None);
            contribution.OverlayEntries.ShouldBeEmpty();
            contributor.Collisions.ShouldContain(item => item.Contains("BundleInvalid(BundleNameCollision)", StringComparison.Ordinal));

            var command = new BundleCommandSource(new HashSet<string>(["review"], StringComparer.Ordinal)).ListCommands(installed, "p").Single();
            command.PaletteId.ShouldBe("/p:review");
            command.Suppressed.ShouldBeTrue();
            command.Report.ShouldBe(BundleCommandSource.NameCollision);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
