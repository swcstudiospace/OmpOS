#pragma warning disable CA2007
#pragma warning disable MA0004
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
        using var store = new FileComputerUseSettingsStore(_root);
        await store.SaveAsync(new ComputerUseUserSettings
        {
            KillChord = "Ctrl+Alt+Esc",
            SecurityPhrase = "pine",
            AuditThumbnails = false,
            DenylistAdditions = ["notes"],
        }, CancellationToken.None);

        using var reloaded = new FileComputerUseSettingsStore(_root);
        var read = await reloaded.LoadAsync(CancellationToken.None);
        read.KillChord.ShouldBe("Ctrl+Alt+Esc");
        read.SecurityPhrase.ShouldBe("pine");
        read.DenylistAdditions.ShouldBe(["notes"]);
        store.LastFailure.ShouldBeNull();
    }

    [Fact]
    public async Task TheFileIsOwnerOnly()
    {
        using var store = new FileComputerUseSettingsStore(_root);
        await store.SaveAsync(new ComputerUseUserSettings(), CancellationToken.None);

        File.GetUnixFileMode(store.FilePath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public async Task AnOversizeFileYieldsDefaults()
    {
        using var store = new FileComputerUseSettingsStore(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        await File.WriteAllBytesAsync(store.FilePath, new byte[FileComputerUseSettingsStore.MaxBytes + 1], TestContext.Current.CancellationToken);

        var read = await store.LoadAsync(CancellationToken.None);
        read.KillChord.ShouldBeNull();
        store.LastFailure.ShouldNotBeNull();
    }

    [Fact]
    public async Task AMalformedFileYieldsDefaults()
    {
        using var store = new FileComputerUseSettingsStore(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        await File.WriteAllTextAsync(store.FilePath, "{", Encoding.UTF8, TestContext.Current.CancellationToken);

        var read = await store.LoadAsync(CancellationToken.None);
        read.SecurityPhrase.ShouldBeNull();
        store.LastFailure.ShouldNotBeNull().ShouldContain("malformed");
    }

    [Fact]
    public async Task DisposingTheStoreTwiceRejectsFurtherLoadsAndSavesExplicitly()
    {
        using var store = new FileComputerUseSettingsStore(_root);
        var settings = new ComputerUseUserSettings { SecurityPhrase = "before-disposal" };
        await store.SaveAsync(settings, CancellationToken.None);
        (await store.LoadAsync(CancellationToken.None)).SecurityPhrase.ShouldBe("before-disposal");

        store.Dispose();
        store.Dispose();

        var loadFailure = await Should.ThrowAsync<ObjectDisposedException>(async () =>
        {
            await store.LoadAsync(CancellationToken.None);
        });
        var saveFailure = await Should.ThrowAsync<ObjectDisposedException>(async () =>
        {
            await store.SaveAsync(settings, CancellationToken.None);
        });
        loadFailure.ObjectName.ShouldBe(typeof(FileComputerUseSettingsStore).FullName);
        saveFailure.ObjectName.ShouldBe(typeof(FileComputerUseSettingsStore).FullName);

        using var reloaded = new FileComputerUseSettingsStore(_root);
        (await reloaded.LoadAsync(CancellationToken.None)).SecurityPhrase.ShouldBe("before-disposal");
    }

    [Fact]
    public async Task CancelledOperationsFinishBeforeDisposalAndLeaveTheStoreUsable()
    {
        using var store = new FileComputerUseSettingsStore(_root);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var settings = new ComputerUseUserSettings { KillChord = "Ctrl+Alt+Esc" };

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await store.LoadAsync(cancellation.Token);
        });
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await store.SaveAsync(settings, cancellation.Token);
        });

        File.Exists(store.FilePath).ShouldBeFalse();
        await store.SaveAsync(settings, CancellationToken.None);
        (await store.LoadAsync(CancellationToken.None)).KillChord.ShouldBe("Ctrl+Alt+Esc");
        store.LastFailure.ShouldBeNull();
    }
}

#pragma warning restore CA2007
#pragma warning restore MA0004
