using Autonogrammer.Api.Services;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Services;

/// <summary>The account-to-folder mapping: a hashed, single-segment directory name, resolved under an absolute base only.</summary>
public sealed class AccountWorkspaceTests
{
    /// <summary>Verifies the directory name is 64 lowercase hexadecimal characters, the SHA-256 digest of the account id, and stable across calls.</summary>
    [Fact]
    public void DirectoryName_IsA64CharLowercaseDigest_AndDeterministic()
    {
        var name = AccountWorkspace.DirectoryName("abc");

        name.ShouldBe("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
        name.ShouldMatch("^[0-9a-f]{64}$");
        AccountWorkspace.DirectoryName("abc").ShouldBe(name);
    }

    /// <summary>Verifies two different accounts map to two different directory names.</summary>
    [Fact]
    public void DifferentAccounts_GetDifferentDirectoryNames()
    {
        AccountWorkspace.DirectoryName("acct-alice").ShouldNotBe(AccountWorkspace.DirectoryName("acct-bob"), StringComparer.Ordinal);
    }

    /// <summary>Verifies the resolved account directory is the absolute base joined with the directory name.</summary>
    [Fact]
    public void Resolve_JoinsTheBaseWithTheDirectoryName()
    {
        var root = Path.Combine(Path.GetTempPath(), "srv", "ws");

        AccountWorkspace.Resolve(root, "acct-alice").ShouldBe(Path.Combine(root, AccountWorkspace.DirectoryName("acct-alice")));
    }

    /// <summary>Verifies a relative base resolves to null rather than to a path that depends on the current directory.</summary>
    [Fact]
    public void Resolve_ReturnsNull_ForARelativeBase()
    {
        AccountWorkspace.Resolve(Path.Combine("relative", "ws"), "acct-alice").ShouldBeNull();
    }
}
