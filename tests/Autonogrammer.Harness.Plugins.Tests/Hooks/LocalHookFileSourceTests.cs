using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Verifies the local file access of the hook loader against the real file system.
/// </summary>
public sealed class LocalHookFileSourceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnExistingFileIsInspectedReadAndDigested()
    {
        using var directory = new TempDirectory();
        var path = directory.PathOf("hooks.json");
        await File.WriteAllTextAsync(path, "abc", Token);
        var source = new LocalHookFileSource();

        var info = source.Inspect(path);

        info.Exists.ShouldBeTrue();
        info.Length.ShouldBe(3);
        Encoding.UTF8.GetString((await source.ReadAsync(path, 100, Token))!).ShouldBe("abc");
        (await source.DigestAsync(path, 100, Token)).ShouldBe(Convert.ToHexStringLower(SHA256.HashData("abc"u8)));
    }

    [Fact]
    public async Task MissingDirectoryAndOversizedPathsReadAsAbsent()
    {
        using var directory = new TempDirectory();
        var big = directory.PathOf("big.json");
        await File.WriteAllTextAsync(big, new string('a', 50), Token);
        var source = new LocalHookFileSource();

        source.Inspect(directory.PathOf("nope.json")).Exists.ShouldBeFalse();
        source.Inspect(directory.Path).Exists.ShouldBeFalse();
        (await source.ReadAsync(directory.PathOf("nope.json"), 100, Token)).ShouldBeNull();
        (await source.ReadAsync(big, 10, Token)).ShouldBeNull();
        (await source.DigestAsync(big, 10, Token)).ShouldBeNull();
    }

    [SupportedOSPlatform("linux")]
    [Fact]
    public async Task AFileIsTamperResistantOnlyWhenNeitherItNorItsDirectoryIsWritableByOthers()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes only.");
        using var directory = new TempDirectory();
        var inner = directory.PathOf("managed");
        Directory.CreateDirectory(inner);
        var path = Path.Combine(inner, "hooks.json");
        await File.WriteAllTextAsync(path, "{}", Token);
        var source = new LocalHookFileSource();
        File.SetUnixFileMode(inner, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        source.Inspect(path).IsTamperResistant.ShouldBeTrue();

        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupWrite);
        source.Inspect(path).IsTamperResistant.ShouldBeFalse();

        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.SetUnixFileMode(inner, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
        source.Inspect(path).IsTamperResistant.ShouldBeFalse();
    }
}
