using System.Runtime.Versioning;
using System.Text;
using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Native.Composition.ComputerUse;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Computer;

/// <summary>The Computer Use settings file: round trip, owner-only mode, oversize and malformed input.</summary>
[UnsupportedOSPlatform("windows")]
public sealed class FileComputerUseSettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ag-73-06-" + Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task ValuesRoundTrip()
    {
        var store = new FileComputerUseSettingsStore(_root);
        await store.SaveAsync(new ComputerUseUserSettings
        {
            KillChord = "Ctrl+Alt+Esc",
            SecurityPhrase = "pine",
            AuditThumbnails = false,
            DenylistAdditions = ["notes"],
        }, CancellationToken.None);

        var read = await new FileComputerUseSettingsStore(_root).LoadAsync(CancellationToken.None);
        read.KillChord.ShouldBe("Ctrl+Alt+Esc");
        read.SecurityPhrase.ShouldBe("pine");
        read.DenylistAdditions.ShouldBe(["notes"]);
        store.LastFailure.ShouldBeNull();
    }

    [Fact]
    public async Task TheFileIsOwnerOnly()
    {
        var store = new FileComputerUseSettingsStore(_root);
        await store.SaveAsync(new ComputerUseUserSettings(), CancellationToken.None);

        File.GetUnixFileMode(store.FilePath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public async Task AnOversizeFileYieldsDefaults()
    {
        var store = new FileComputerUseSettingsStore(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        await File.WriteAllBytesAsync(store.FilePath, new byte[FileComputerUseSettingsStore.MaxBytes + 1]);

        var read = await store.LoadAsync(CancellationToken.None);
        read.KillChord.ShouldBeNull();
        store.LastFailure.ShouldNotBeNull();
    }

    [Fact]
    public async Task AMalformedFileYieldsDefaults()
    {
        var store = new FileComputerUseSettingsStore(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        await File.WriteAllTextAsync(store.FilePath, "{", Encoding.UTF8);

        var read = await store.LoadAsync(CancellationToken.None);
        read.SecurityPhrase.ShouldBeNull();
        store.LastFailure.ShouldContain("malformed");
    }
}
