using OmpOS.Api.Services.WorkspaceSettings;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.WorkspaceSettings;

/// <summary>Which names <see cref="SecretFilePolicy"/> always hides.</summary>
public sealed class SecretFilePolicyTests
{
    /// <summary>Environment files, keys, certificates, credential files and the folders that keep them are secret names, in any letter case.</summary>
    [Theory]
    [InlineData(".env")]
    [InlineData(".ENV")]
    [InlineData(".env.production")]
    [InlineData("server.pem")]
    [InlineData("TLS.KEY")]
    [InlineData("bundle.p12")]
    [InlineData("bundle.pfx")]
    [InlineData("id_rsa")]
    [InlineData("id_ed25519.pub")]
    [InlineData("credentials")]
    [InlineData("credentials.json")]
    [InlineData(".netrc")]
    [InlineData(".git")]
    [InlineData(".ssh")]
    public void SecretNames_AreExcluded(string name)
    {
        SecretFilePolicy.IsSecretName(name).ShouldBeTrue();
    }

    /// <summary>Ordinary names, and names that only contain a secret name, are shown.</summary>
    [Theory]
    [InlineData("README.md")]
    [InlineData("environment.md")]
    [InlineData("key.txt")]
    [InlineData("grid_rsa")]
    [InlineData(".gitignore")]
    [InlineData(".cursor")]
    public void OrdinaryNames_AreShown(string name)
    {
        SecretFilePolicy.IsSecretName(name).ShouldBeFalse();
    }

    /// <summary>A path is secret when any one segment is.</summary>
    [Theory]
    [InlineData("a/b/.env", true)]
    [InlineData(".git/config", true)]
    [InlineData("a\\.ssh\\known_hosts", true)]
    [InlineData("a/b/c.txt", false)]
    public void APath_IsSecret_WhenAnySegmentIs(string path, bool expected)
    {
        SecretFilePolicy.IsSecretPath(path).ShouldBe(expected);
    }
}
