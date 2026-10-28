using Autonogrammer.Presentation.Mentions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Mentions;

/// <summary>Recognising files that usually hold secrets by name.</summary>
public sealed class MentionSecretFilesTests
{
    /// <summary>Env files, keys and credential stores are secret-bearing; example files, public keys and look-alikes are not.</summary>
    [Theory]
    [InlineData(".env", true)]
    [InlineData("config/.env.local", true)]
    [InlineData(".ENV", true)]
    [InlineData(".env.example", false)]
    [InlineData(".env.sample", false)]
    [InlineData("server.pem", true)]
    [InlineData("keys/id_rsa", true)]
    [InlineData("id_ed25519", true)]
    [InlineData("id_rsa.pub", false)]
    [InlineData("cert.key", true)]
    [InlineData("credentials", true)]
    [InlineData("README.md", false)]
    [InlineData("src/environment.cs", false)]
    [InlineData("monkey.cs", false)]
    [InlineData("dir/", false)]
    public void ByName(string path, bool expected) => MentionSecretFiles.IsSecretBearing(path).ShouldBe(expected);
}
