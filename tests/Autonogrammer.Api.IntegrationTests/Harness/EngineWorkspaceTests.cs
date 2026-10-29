using System.Runtime.Versioning;
using Autonogrammer.Api.Harness;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Harness;

/// <summary>The per-session engine working directory: a safe name, created owner-only under the workspace root, never outside it.</summary>
public sealed class EngineWorkspaceTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "autonogrammer-engine-workspace-" + Guid.NewGuid().ToString("N"));

    private string Root => Path.Combine(_base, "workspaces");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_base))
        {
            Directory.Delete(_base, recursive: true);
        }
    }

    /// <summary>Session identifiers made of letters, digits, hyphens and underscores are accepted, up to 128 characters.</summary>
    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef")]
    [InlineData("cloud-0123456789abcdef0123456789abcdef")]
    [InlineData("Session_1-A")]
    [InlineData("a")]
    public void SafeSessionIdsAreAccepted(string sessionId)
    {
        EngineWorkspace.IsValidSessionId(sessionId).ShouldBeTrue();
    }

    /// <summary>Anything that could leave the workspace root, or is not a plain name, is refused.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    [InlineData("a/b")]
    [InlineData("/absolute")]
    [InlineData("a\\b")]
    [InlineData("a.b")]
    [InlineData("C:")]
    [InlineData("a b")]
    [InlineData("a\0b")]
    [InlineData("é")]
    public void UnsafeSessionIdsAreRefused(string? sessionId)
    {
        EngineWorkspace.IsValidSessionId(sessionId).ShouldBeFalse();
    }

    /// <summary>The length limit is 128 characters.</summary>
    [Fact]
    public void SessionIdLengthIsLimited()
    {
        EngineWorkspace.IsValidSessionId(new string('a', EngineWorkspace.MaxSessionIdLength)).ShouldBeTrue();
        EngineWorkspace.IsValidSessionId(new string('a', EngineWorkspace.MaxSessionIdLength + 1)).ShouldBeFalse();
    }

    /// <summary>The session directory is created under the root, including a missing root, and creating it again returns the same path.</summary>
    [Fact]
    public void CreatesTheSessionDirectoryAndIsIdempotent()
    {
        var first = EngineWorkspace.EnsureSessionDirectory(Root, "session-one");
        var second = EngineWorkspace.EnsureSessionDirectory(Root, "session-one");

        first.ShouldBe(Path.Combine(Root, "session-one"));
        second.ShouldBe(first);
        Directory.Exists(first).ShouldBeTrue();
    }

    /// <summary>On Unix a new session directory is readable, writable and searchable by its owner only, and an existing one is tightened.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public void SessionDirectoryIsOwnerOnly()
    {
        SkipUnlessUnix();
        var path = EngineWorkspace.EnsureSessionDirectory(Root, "session-private");
        File.GetUnixFileMode(path).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        _ = EngineWorkspace.EnsureSessionDirectory(Root, "session-private");

        File.GetUnixFileMode(path).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>An existing workspace root keeps the permissions its operator gave it.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public void ExistingRootKeepsItsPermissions()
    {
        SkipUnlessUnix();
        Directory.CreateDirectory(Root);
        var shared = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute;
        File.SetUnixFileMode(Root, shared);

        _ = EngineWorkspace.EnsureSessionDirectory(Root, "session-shared-root");

        File.GetUnixFileMode(Root).ShouldBe(shared);
    }

    /// <summary>A traversal identifier is refused before anything is created, and nothing appears beside the root.</summary>
    [Theory]
    [InlineData("../escape")]
    [InlineData("..")]
    [InlineData("nested/escape")]
    public void TraversalIsRefusedAndCreatesNothing(string sessionId)
    {
        var failure = Should.Throw<ArgumentException>(() => EngineWorkspace.EnsureSessionDirectory(Root, sessionId));

        failure.Message.ShouldNotContain(sessionId, Case.Sensitive);
        Directory.Exists(Root).ShouldBeFalse();
        Directory.Exists(Path.Combine(_base, "escape")).ShouldBeFalse();
    }

    /// <summary>A relative root is refused.</summary>
    [Fact]
    public void RelativeRootIsRefused()
    {
        Should.Throw<ArgumentException>(() => EngineWorkspace.EnsureSessionDirectory("relative/root", "session-one"));
    }

    /// <summary>A session directory that is a symbolic link is refused, so a link cannot redirect the engine elsewhere.</summary>
    [Fact]
    public void LinkedSessionDirectoryIsRefused()
    {
        var target = Path.Combine(_base, "elsewhere");
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(Root);
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(Root, "session-link"), target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Skip("This account cannot create symbolic links.");
        }

        Should.Throw<ProcessStartHarnessException>(() => EngineWorkspace.EnsureSessionDirectory(Root, "session-link"));
    }

    private static void SkipUnlessUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("File modes are a Unix concept.");
        }
    }
}
