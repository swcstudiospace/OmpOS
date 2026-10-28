using System.Reflection;
using System.Text.Json;
using OmpOS.Contracts.Environments;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>The shape of the environment contracts: what they store and, above all, what they cannot store.</summary>
public sealed class EnvironmentContractTests
{
    private static readonly string[] SecretWords =
    [
        "password", "passphrase", "secret", "token", "privatekey", "keymaterial", "credential", "apikey", "bearer",
    ];

    /// <summary>No public type of the environment contracts has a property that could carry a secret.</summary>
    [Fact]
    public void NoContractPropertyCanCarryASecret()
    {
        var types = typeof(SshProfile).Assembly.GetTypes()
            .Where(type => type.IsPublic && string.Equals(type.Namespace, typeof(SshProfile).Namespace, StringComparison.Ordinal))
            .ToList();

        types.ShouldContain(typeof(SshProfile));
        types.ShouldContain(typeof(EnvironmentProfile));
        types.Count.ShouldBeGreaterThan(8);
        foreach (var type in types)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                foreach (var word in SecretWords)
                {
                    property.Name.ShouldNotContain(word, Case.Insensitive, $"{type.Name}.{property.Name} looks secret-bearing");
                }
            }
        }
    }

    /// <summary>A profile survives a round trip through the source-generated JSON context, with enums as strings.</summary>
    [Fact]
    public void AProfileRoundTripsThroughJson()
    {
        var profile = new EnvironmentProfile
        {
            Id = "work-box",
            DisplayName = "Work box",
            Kind = EnvironmentKind.Ssh,
            Ssh = new SshProfile
            {
                Alias = "work",
                Hostname = "10.0.0.5",
                Port = 2222,
                User = "dev",
                IdentityFile = "/home/me/.ssh/id_ed25519",
                ProxyJump = "bastion",
                Options = new Dictionary<string, string>(StringComparer.Ordinal) { ["Compression"] = "yes" },
                RemoteWorkspaceRoot = "/home/dev/work",
                RemoteOmpPath = "/opt/omp/bin/omp",
                ForwardAgent = true,
                KeepAliveSeconds = 30,
            },
        };

        var json = JsonSerializer.Serialize(profile, EnvironmentJsonContext.Default.EnvironmentProfile);
        var back = JsonSerializer.Deserialize(json, EnvironmentJsonContext.Default.EnvironmentProfile);

        json.ShouldContain("\"kind\":\"Ssh\"", Case.Sensitive);
        back.ShouldNotBeNull();
        back.Id.ShouldBe("work-box");
        back.Kind.ShouldBe(EnvironmentKind.Ssh);
        back.Ssh.ShouldNotBeNull();
        back.Ssh.Alias.ShouldBe("work");
        back.Ssh.Port.ShouldBe(2222);
        back.Ssh.Options["Compression"].ShouldBe("yes");
        back.Ssh.ForwardAgent.ShouldBeTrue();
        back.Ssh.KeepAliveSeconds.ShouldBe(30);
        EnvironmentProfileValidator.Validate(back).ShouldBeEmpty();
    }

    /// <summary>A kind this build does not know is a deserialization failure, never a profile that silently runs somewhere else.</summary>
    [Fact]
    public void AnUnknownKindIsRejectedWhenReadingJson()
    {
        const string Json = "{\"id\":\"x\",\"displayName\":\"X\",\"kind\":\"Mainframe\"}";

        Should.Throw<JsonException>(() => JsonSerializer.Deserialize(Json, EnvironmentJsonContext.Default.EnvironmentProfile));
    }

    /// <summary>Defaults: no agent forwarding, a 15 second keep-alive, no options.</summary>
    [Fact]
    public void DefaultsAreTheSafeOnes()
    {
        var profile = new SshProfile { Alias = "a", RemoteWorkspaceRoot = "/w" };

        profile.ForwardAgent.ShouldBeFalse();
        profile.KeepAliveSeconds.ShouldBe(SshProfileRules.DefaultKeepAliveSeconds);
        profile.Options.ShouldBeEmpty();
        profile.Hostname.ShouldBeNull();
        profile.RemoteOmpPath.ShouldBeNull();
    }
}
