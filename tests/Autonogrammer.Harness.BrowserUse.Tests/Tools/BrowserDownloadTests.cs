using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Harness.BrowserUse.Tools;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Tools;

/// <summary>Downloads are denied unless quarantine rules all hold and a local card releases the file.</summary>
public sealed class BrowserDownloadTests
{
    [Fact]
    public void Deny_RefusesEveryDownload()
    {
        var quarantine = new BrowserDownloadQuarantine();
        var profile = NewProfile();
        var stored = quarantine.TryStore(DownloadMode.Deny, profile, "notes.txt", "hello"u8);
        stored.ShouldBeNull();
        quarantine.LastRefusal.ShouldBe("DownloadDenied");
        Directory.Exists(Path.Combine(profile, "downloads")).ShouldBeFalse();
    }

    [Fact]
    public void Quarantine_IsOwnerOnly_Capped_AndReleasedOnlyAfterMustBeLocal()
    {
        var quarantine = new BrowserDownloadQuarantine();
        var profile = NewProfile();
        var exe = quarantine.TryStore(DownloadMode.Quarantine, profile, "payload.exe", "no"u8);
        exe.ShouldBeNull();
        var huge = new byte[1];
        quarantine.TryStore(DownloadMode.Quarantine, profile, "big.bin", new byte[(int)BrowserDownloadQuarantine.MaxBytes + 1]).ShouldBeNull();
        _ = huge;
        var stored = quarantine.TryStore(DownloadMode.Quarantine, profile, "notes.txt", "hello"u8);
        stored.ShouldNotBeNull();
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(Path.GetDirectoryName(stored)!).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        var workspace = NewProfile();
        BrowserDownloadQuarantine.Release(stored, workspace, released: false).ShouldBeNull();
        File.Exists(Path.Combine(workspace, "notes.txt")).ShouldBeFalse();
        var released = BrowserDownloadQuarantine.Release(stored, workspace, released: true);
        released.ShouldNotBeNull();
        File.ReadAllText(released).ShouldBe("hello");
    }

    private static string NewProfile()
    {
        var path = Path.Combine(Path.GetTempPath(), "ag-dl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
