using OmpOS.Contracts.Environments;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>The rules that keep a hostile alias, path or option out of an ssh argument list and a remote shell line.</summary>
public sealed class EnvironmentProfileValidatorTests
{
    private static SshProfile Valid() => new() { Alias = "build-box", RemoteWorkspaceRoot = "/home/dev/work" };

    /// <summary>A plain profile passes with no issue.</summary>
    [Fact]
    public void AValidProfilePasses()
    {
        EnvironmentProfileValidator.Validate(Valid()).ShouldBeEmpty();
    }

    /// <summary>Aliases that are ordinary host names pass.</summary>
    [Theory]
    [InlineData("build-box")]
    [InlineData("user@host.example")]
    [InlineData("a_b.c")]
    [InlineData("10.0.0.7")]
    public void OrdinaryAliasesAreAccepted(string alias)
    {
        SshProfileRules.IsValidAlias(alias).ShouldBeTrue();
    }

    /// <summary>An alias that could be read as an option, split into words or expanded by a shell is refused.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("-oProxyCommand=touch /tmp/x")]
    [InlineData("-J")]
    [InlineData("a b")]
    [InlineData("a;b")]
    [InlineData("a$(id)")]
    [InlineData("a`id`")]
    [InlineData("a\nb")]
    [InlineData("a'b")]
    [InlineData("a/b")]
    public void HostileAliasesAreRefused(string alias)
    {
        SshProfileRules.IsValidAlias(alias).ShouldBeFalse();
        var profile = Valid() with { Alias = alias };
        EnvironmentProfileValidator.Validate(profile).ShouldContain(issue => issue.Property == "Ssh.Alias");
    }

    /// <summary>An alias longer than 128 characters is refused.</summary>
    [Fact]
    public void AnOverlongAliasIsRefused()
    {
        SshProfileRules.IsValidAlias(new string('a', 129)).ShouldBeFalse();
        SshProfileRules.IsValidAlias(new string('a', 128)).ShouldBeTrue();
    }

    /// <summary>A remote path must be absolute and free of control characters and parent segments.</summary>
    [Theory]
    [InlineData("/home/dev/work", true)]
    [InlineData("/home/dev/my project", true)]
    [InlineData("/", true)]
    [InlineData("relative/path", false)]
    [InlineData("~/work", false)]
    [InlineData("", false)]
    [InlineData("/a/../b", false)]
    [InlineData("/a\nb", false)]
    [InlineData("/a\rb", false)]
    [InlineData("/a\0b", false)]
    [InlineData("/a\tb", false)]
    public void RemotePathsMustBeAbsoluteAndPlain(string path, bool expected)
    {
        SshProfileRules.IsValidAbsoluteRemotePath(path).ShouldBe(expected);
        EnvironmentProfileValidator.Validate(Valid() with { RemoteWorkspaceRoot = path }).Any(issue => string.Equals(issue.Property, "Ssh.RemoteWorkspaceRoot", StringComparison.Ordinal)).ShouldBe(!expected);
    }

    /// <summary>The remote omp is a plain command name or an absolute path, never a flag or a shell fragment.</summary>
    [Theory]
    [InlineData("omp", true)]
    [InlineData("/opt/omp/bin/omp", true)]
    [InlineData("omp-18.12", true)]
    [InlineData("-e", false)]
    [InlineData("omp; id", false)]
    [InlineData("omp --version", false)]
    [InlineData("./omp", false)]
    public void RemoteOmpPathMustBeAPlainExecutable(string path, bool expected)
    {
        SshProfileRules.IsValidRemoteExecutable(path).ShouldBe(expected);
    }

    /// <summary>Only allowlisted options with accepted values pass; nothing that runs a command or weakens host key checking does.</summary>
    [Theory]
    [InlineData("Compression", "yes", true)]
    [InlineData("compression", "no", true)]
    [InlineData("ConnectTimeout", "20", true)]
    [InlineData("ConnectTimeout", "0", false)]
    [InlineData("ConnectTimeout", "abc", false)]
    [InlineData("StrictHostKeyChecking", "yes", true)]
    [InlineData("StrictHostKeyChecking", "ask", true)]
    [InlineData("StrictHostKeyChecking", "no", false)]
    [InlineData("StrictHostKeyChecking", "accept-new", false)]
    [InlineData("ProxyCommand", "nc %h %p", false)]
    [InlineData("LocalCommand", "id", false)]
    [InlineData("PermitLocalCommand", "yes", false)]
    [InlineData("RemoteCommand", "id", false)]
    [InlineData("SendEnv", "*", false)]
    [InlineData("LocalForward", "1:2:3", false)]
    [InlineData("BatchMode", "no", false)]
    [InlineData("PreferredAuthentications", "publickey,gssapi-with-mic", true)]
    [InlineData("PreferredAuthentications", "password", false)]
    public void OptionsAreChecked(string name, string value, bool expected)
    {
        SshProfileRules.IsAllowedOption(name, value).ShouldBe(expected);
        var issues = EnvironmentProfileValidator.Validate(Valid() with { Options = new Dictionary<string, string>(StringComparer.Ordinal) { [name] = value } });
        issues.Any(issue => string.Equals(issue.Property, "Ssh.Options", StringComparison.Ordinal)).ShouldBe(!expected);
    }

    /// <summary>The same option written twice in different cases is refused.</summary>
    [Fact]
    public void ADuplicateOptionIsRefused()
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal) { ["Compression"] = "yes", ["compression"] = "no" };
        EnvironmentProfileValidator.Validate(Valid() with { Options = options }).ShouldContain(issue => issue.Property == "Ssh.Options");
    }

    /// <summary>Host names, users, ports and jump hosts are checked.</summary>
    [Fact]
    public void CustomHostFieldsAreChecked()
    {
        var good = Valid() with { Hostname = "10.1.2.3", Port = 2222, User = "dev", IdentityFile = "/home/me/.ssh/id_ed25519", ProxyJump = "jump@bastion.example:2200,other" };
        EnvironmentProfileValidator.Validate(good).ShouldBeEmpty();

        EnvironmentProfileValidator.Validate(Valid() with { Hostname = "-oProxyCommand=x" }).ShouldContain(issue => issue.Property == "Ssh.Hostname");
        EnvironmentProfileValidator.Validate(Valid() with { Port = 0 }).ShouldContain(issue => issue.Property == "Ssh.Port");
        EnvironmentProfileValidator.Validate(Valid() with { Port = 65536 }).ShouldContain(issue => issue.Property == "Ssh.Port");
        EnvironmentProfileValidator.Validate(Valid() with { User = "-l" }).ShouldContain(issue => issue.Property == "Ssh.User");
        EnvironmentProfileValidator.Validate(Valid() with { IdentityFile = "-oProxyCommand=x" }).ShouldContain(issue => issue.Property == "Ssh.IdentityFile");
        EnvironmentProfileValidator.Validate(Valid() with { IdentityFile = "/a\nb" }).ShouldContain(issue => issue.Property == "Ssh.IdentityFile");
        EnvironmentProfileValidator.Validate(Valid() with { ProxyJump = "a,-oProxyCommand=x" }).ShouldContain(issue => issue.Property == "Ssh.ProxyJump");
        EnvironmentProfileValidator.Validate(Valid() with { ProxyJump = "ssh://host" }).ShouldContain(issue => issue.Property == "Ssh.ProxyJump");
    }

    /// <summary>The keep-alive interval has bounds.</summary>
    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(600, true)]
    [InlineData(601, false)]
    public void KeepAliveHasBounds(int seconds, bool expected)
    {
        EnvironmentProfileValidator.Validate(Valid() with { KeepAliveSeconds = seconds }).Any(issue => string.Equals(issue.Property, "Ssh.KeepAliveSeconds", StringComparison.Ordinal)).ShouldBe(!expected);
    }

    /// <summary>An environment profile needs its ssh details exactly when it is an ssh environment.</summary>
    [Fact]
    public void SshDetailsBelongToSshEnvironmentsOnly()
    {
        var ssh = new EnvironmentProfile { Id = "work-box", DisplayName = "Work box", Kind = EnvironmentKind.Ssh, Ssh = Valid() };
        EnvironmentProfileValidator.Validate(ssh).ShouldBeEmpty();

        EnvironmentProfileValidator.Validate(ssh with { Ssh = null }).ShouldContain(issue => issue.Property == "Ssh");
        EnvironmentProfileValidator.Validate(ssh with { Kind = EnvironmentKind.Local }).ShouldContain(issue => issue.Property == "Ssh");
        EnvironmentProfileValidator.Validate(new EnvironmentProfile { Id = "local", DisplayName = "This machine", Kind = EnvironmentKind.Local }).ShouldBeEmpty();
    }

    /// <summary>Identifiers and display names are checked, and an unknown kind is refused.</summary>
    [Fact]
    public void IdentifiersAndNamesAreChecked()
    {
        var profile = new EnvironmentProfile { Id = "local", DisplayName = "This machine", Kind = EnvironmentKind.Local };
        EnvironmentProfileValidator.Validate(profile with { Id = "-bad" }).ShouldContain(issue => issue.Property == "Id");
        EnvironmentProfileValidator.Validate(profile with { Id = "has space" }).ShouldContain(issue => issue.Property == "Id");
        EnvironmentProfileValidator.Validate(profile with { DisplayName = " " }).ShouldContain(issue => issue.Property == "DisplayName");
        EnvironmentProfileValidator.Validate(profile with { DisplayName = "a\nb" }).ShouldContain(issue => issue.Property == "DisplayName");
        EnvironmentProfileValidator.Validate(profile with { Kind = (EnvironmentKind)99 }).ShouldContain(issue => issue.Property == "Kind");
    }

    /// <summary>An issue names the property and never repeats the rejected value.</summary>
    [Fact]
    public void IssuesNeverRepeatTheRejectedValue()
    {
        const string Hostile = "-oProxyCommand=cat /etc/passwd";
        var issues = EnvironmentProfileValidator.Validate(Valid() with { Alias = Hostile, Hostname = Hostile, User = Hostile, IdentityFile = Hostile, ProxyJump = Hostile, RemoteOmpPath = Hostile });

        issues.Count.ShouldBeGreaterThan(0);
        foreach (var issue in issues)
        {
            issue.Message.ShouldNotContain("passwd", Case.Sensitive);
        }
    }
}
