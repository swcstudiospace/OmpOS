using System.Diagnostics;
using Autonogrammer.Contracts.Environments;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>Resolving an alias through <c>ssh -G</c> and parsing what ssh prints.</summary>
public sealed class SshConfigResolverTests
{
    private const string JumpBoxConfig =
        "host jump-box\nuser deploy\nhostname 10.0.0.5\nport 2222\nproxyjump bastion.example\nidentityfile ~/.ssh/id_ed25519\nidentityfile ~/.ssh/id_rsa\n" +
        "forwardagent no\nstricthostkeychecking ask\nuserknownhostsfile ~/.ssh/known_hosts ~/.ssh/known_hosts2\nproxycommand none\nsendenv LANG\n";

    /// <summary>The key and value lines of <c>ssh -G</c> become a typed configuration; <c>none</c> means unset.</summary>
    [Fact]
    public void TheOutputOfSshGIsParsed()
    {
        var config = SshEffectiveConfig.Parse("jump-box", JumpBoxConfig);

        config.Alias.ShouldBe("jump-box");
        config.Hostname.ShouldBe("10.0.0.5");
        config.Port.ShouldBe(2222);
        config.User.ShouldBe("deploy");
        config.ProxyJump.ShouldBe("bastion.example");
        config.ProxyCommand.ShouldBeNull();
        config.IdentityFiles.ShouldBe(["~/.ssh/id_ed25519", "~/.ssh/id_rsa"]);
        config.ForwardAgent.ShouldBeFalse();
        config.StrictHostKeyChecking.ShouldBe("ask");
        config.UserKnownHostsFiles.ShouldBe(["~/.ssh/known_hosts", "~/.ssh/known_hosts2"]);
        config.Values["sendenv"].ShouldBe(["LANG"]);
    }

    /// <summary>Keys are lower-cased, blank lines are skipped, a key without value is kept and a missing key reads as null.</summary>
    [Fact]
    public void ParsingIsTolerant()
    {
        var config = SshEffectiveConfig.Parse("x", "\nHostName  h.example  \n\nBatchMode\nProxyJump none\nForwardAgent yes\nPort notanumber\n");

        config.Hostname.ShouldBe("h.example");
        config.Values["batchmode"].ShouldBe([string.Empty]);
        config.ProxyJump.ShouldBeNull();
        config.ForwardAgent.ShouldBeTrue();
        config.Port.ShouldBeNull();
        config.User.ShouldBeNull();
        config.IdentityFiles.ShouldBeEmpty();
    }

    /// <summary>The resolver runs <c>ssh -G -- alias</c> and parses the answer.</summary>
    [Fact]
    public async Task TheResolverRunsSshGWithADoubleDash()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create(new FakeRemoteHostOptions { CannedConfigs = new Dictionary<string, string>(StringComparer.Ordinal) { ["jump-box"] = JumpBoxConfig } });

        var resolution = await new SshConfigResolver(host.ClientOptions).ResolveAsync("jump-box", ct).ConfigureAwait(true);

        resolution.Failure.ShouldBeNull();
        resolution.Config.ShouldNotBeNull().Hostname.ShouldBe("10.0.0.5");
        var call = host.SshCalls().ShouldHaveSingleItem();
        call.ToArray().ShouldBe(["-G", "--", "jump-box"]);
    }

    /// <summary>A configuration file option is passed with <c>-F</c>.</summary>
    [Fact]
    public async Task AConfigFileIsPassedToSshG()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create();
        var options = host.ClientOptions with { ConfigFile = "/etc/autonogrammer/ssh_config" };

        _ = await new SshConfigResolver(options).ResolveAsync("any", ct).ConfigureAwait(true);

        host.SshCalls().ShouldHaveSingleItem().ToArray().ShouldBe(["-F", "/etc/autonogrammer/ssh_config", "-G", "--", "any"]);
    }

    /// <summary>An alias that could be read as an option never reaches ssh.</summary>
    [Theory]
    [InlineData("-oProxyCommand=touch /tmp/pwned")]
    [InlineData("a b")]
    [InlineData("")]
    public async Task AHostileAliasIsNeverPassedToSsh(string alias)
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = FakeRemoteHost.Create();

        await Should.ThrowAsync<ArgumentException>(() => new SshConfigResolver(host.ClientOptions).ResolveAsync(alias, ct)).ConfigureAwait(true);

        host.SshCalls().ShouldBeEmpty();
    }

    /// <summary>A missing ssh client is a diagnosis, not an exception.</summary>
    [Fact]
    public async Task AMissingSshClientIsADiagnosis()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new SshClientOptions { SshPath = "/nonexistent/autonogrammer-test-ssh" };

        var resolution = await new SshConfigResolver(options).ResolveAsync("any", ct).ConfigureAwait(true);

        resolution.Config.ShouldBeNull();
        resolution.Failure.ShouldNotBeNull().Kind.ShouldBe(RemoteDiagnosisKind.SshMissing);
    }

    /// <summary>The real ssh evaluates Include, Match and ProxyJump itself; the resolver reports what ssh decided.</summary>
    [Fact]
    public async Task RealSshEvaluatesIncludeMatchAndProxyJump()
    {
        var ct = TestContext.Current.CancellationToken;
        if (!SshIsInstalled())
        {
            Assert.Skip("ssh is not installed; the real ssh -G test is skipped.");
        }

        using var directory = TempDirectory.Create();
        var included = directory.Combine("included.conf");
        await File.WriteAllTextAsync(included, "Host web\n  Port 2201\n  IdentityFile /keys/web_ed25519\n", TestContext.Current.CancellationToken).ConfigureAwait(true);
        var config = directory.Combine("config");
        await File.WriteAllTextAsync(
            config,
            $"Include {included}\n" +
            "Match originalhost web\n  User matched-user\n" +
            "Host web\n  HostName web.example.com\n" +
            "Host *\n  ProxyJump bastion.example.com\n  User fallback-user\n", TestContext.Current.CancellationToken).ConfigureAwait(true);
        var options = new SshClientOptions { ConfigFile = config };

        var resolution = await new SshConfigResolver(options).ResolveAsync("web", ct).ConfigureAwait(true);

        resolution.Failure.ShouldBeNull();
        var effective = resolution.Config.ShouldNotBeNull();
        effective.Hostname.ShouldBe("web.example.com");
        effective.Port.ShouldBe(2201);
        effective.User.ShouldBe("matched-user");
        effective.ProxyJump.ShouldBe("bastion.example.com");
        effective.IdentityFiles.ShouldContain("/keys/web_ed25519", StringComparer.Ordinal);
    }

    private static bool SshIsInstalled()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("ssh", "-V") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false });
            process?.WaitForExit();
            return process is not null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
