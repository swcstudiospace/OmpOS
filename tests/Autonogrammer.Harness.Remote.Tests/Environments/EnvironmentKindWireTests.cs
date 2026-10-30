using System.Reflection;
using System.Text.Json;
using Autonogrammer.Contracts.Environments;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>The stored and wire shape of the environment kinds: stable numbers, names on the wire, unknown kinds that survive reading, and no container runtime but podman.</summary>
public sealed class EnvironmentKindWireTests
{
    /// <summary>The numeric values of the kinds never change: stored files and other builds depend on them.</summary>
    [Fact]
    public void TheNumericValuesAreStable()
    {
        ((int)EnvironmentKind.Local).ShouldBe(0);
        ((int)EnvironmentKind.Ssh).ShouldBe(1);
        ((int)EnvironmentKind.Cloud).ShouldBe(2);
        ((int)EnvironmentKind.Container).ShouldBe(3);
        ((int)EnvironmentKind.Wsl).ShouldBe(4);
    }

    /// <summary>A container profile round-trips through the source-generated context with the kind as a name.</summary>
    [Fact]
    public void AContainerProfileRoundTrips()
    {
        var profile = new EnvironmentProfile
        {
            Id = "container-devbox",
            DisplayName = "Dev box",
            Kind = EnvironmentKind.Container,
            Container = new ContainerProfile { Container = "devbox", RemoteUser = "dev", WorkspaceFolder = "/workspaces/app", PodmanPath = "/usr/bin/podman", OmpPath = "/opt/omp" },
        };

        var json = JsonSerializer.Serialize(profile, EnvironmentJsonContext.Default.EnvironmentProfile);
        var back = JsonSerializer.Deserialize(json, EnvironmentJsonContext.Default.EnvironmentProfile);

        json.ShouldContain("\"kind\":\"Container\"", Case.Sensitive);
        back.ShouldNotBeNull();
        back.Kind.ShouldBe(EnvironmentKind.Container);
        back.Container.ShouldBe(profile.Container);
        back.Wsl.ShouldBeNull();
        back.Ssh.ShouldBeNull();
        EnvironmentProfileValidator.Validate(back).ShouldBeEmpty();
    }

    /// <summary>A WSL profile round-trips through the source-generated context with the kind as a name.</summary>
    [Fact]
    public void AWslProfileRoundTrips()
    {
        var profile = new EnvironmentProfile
        {
            Id = "wsl-ubuntu",
            DisplayName = "Ubuntu",
            Kind = EnvironmentKind.Wsl,
            Wsl = new WslProfile { Distro = "Ubuntu-24.04", User = "dev", WorkspacePath = "/home/dev/app" },
        };

        var json = JsonSerializer.Serialize(profile, EnvironmentJsonContext.Default.EnvironmentProfile);
        var back = JsonSerializer.Deserialize(json, EnvironmentJsonContext.Default.EnvironmentProfile);

        json.ShouldContain("\"kind\":\"Wsl\"", Case.Sensitive);
        back.ShouldNotBeNull();
        back.Wsl.ShouldBe(profile.Wsl);
        EnvironmentProfileValidator.Validate(back).ShouldBeEmpty();
    }

    /// <summary>A kind written as its number is read as that kind.</summary>
    [Theory]
    [InlineData(0, EnvironmentKind.Local)]
    [InlineData(1, EnvironmentKind.Ssh)]
    [InlineData(3, EnvironmentKind.Container)]
    [InlineData(4, EnvironmentKind.Wsl)]
    public void ANumberedKindIsRead(int number, EnvironmentKind expected)
    {
        var json = $"{{\"id\":\"x\",\"displayName\":\"X\",\"kind\":{number}}}";

        JsonSerializer.Deserialize(json, EnvironmentJsonContext.Default.EnvironmentProfile).ShouldNotBeNull().Kind.ShouldBe(expected);
    }

    /// <summary>
    /// An unknown kind, by name or by number, still deserializes (a newer build may write it), is rejected by the validator, and is written back as an undefined number
    /// that the next read keeps (the original name of an unknown kind is not preserved).
    /// </summary>
    [Theory]
    [InlineData("\"Devbox\"")]
    [InlineData("99")]
    [InlineData("-5")]
    public void AnUnknownKindSurvivesReadingAndIsRejected(string kindJson)
    {
        var json = $"{{\"id\":\"x\",\"displayName\":\"X\",\"kind\":{kindJson}}}";

        var profile = JsonSerializer.Deserialize(json, EnvironmentJsonContext.Default.EnvironmentProfile);

        profile.ShouldNotBeNull();
        Enum.IsDefined(profile.Kind).ShouldBeFalse();
        EnvironmentProfileValidator.Validate(profile).ShouldContain(issue => issue.Property == "Kind");
        var again = JsonSerializer.Deserialize(JsonSerializer.Serialize(profile, EnvironmentJsonContext.Default.EnvironmentProfile), EnvironmentJsonContext.Default.EnvironmentProfile);
        again.ShouldNotBeNull().Kind.ShouldBe(profile.Kind);
    }

    /// <summary>A list in which one profile has an unknown kind still reads the others.</summary>
    [Fact]
    public void OneUnknownKindDoesNotHideTheOtherProfiles()
    {
        const string Json = "[{\"id\":\"a\",\"displayName\":\"A\",\"kind\":\"Local\"},{\"id\":\"b\",\"displayName\":\"B\",\"kind\":\"Hologram\"},{\"id\":\"c\",\"displayName\":\"C\",\"kind\":\"Wsl\",\"wsl\":{\"distro\":\"Debian\",\"workspacePath\":\"/w\"}}]";

        var profiles = JsonSerializer.Deserialize(Json, EnvironmentJsonContext.Default.EnvironmentProfileArray);

        profiles.ShouldNotBeNull();
        profiles.Select(profile => EnvironmentProfileValidator.IsValid(profile)).ShouldBe([true, false, true]);
    }

    /// <summary>A kind that is neither a string nor a number is a document error.</summary>
    [Fact]
    public void ABooleanKindIsADocumentError()
    {
        const string Json = "{\"id\":\"x\",\"displayName\":\"X\",\"kind\":true}";

        Should.Throw<JsonException>(() => JsonSerializer.Deserialize(Json, EnvironmentJsonContext.Default.EnvironmentProfile));
    }

    /// <summary>The container runtime is podman only: no public member of the environment contracts names another runtime.</summary>
    [Fact]
    public void NoContractNamesAnotherContainerRuntime()
    {
        var banned = string.Concat("dock", "er");
        var types = typeof(ContainerProfile).Assembly.GetTypes()
            .Where(type => type.IsPublic && string.Equals(type.Namespace, typeof(ContainerProfile).Namespace, StringComparison.Ordinal))
            .ToList();

        types.ShouldContain(typeof(ContainerProfile));
        types.ShouldContain(typeof(WslProfile));
        foreach (var type in types)
        {
            type.Name.ShouldNotContain(banned, Case.Insensitive);
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                member.Name.ShouldNotContain(banned, Case.Insensitive, $"{type.Name}.{member.Name}");
            }
        }
    }

    /// <summary>The new profile types have no property that could carry a secret.</summary>
    [Fact]
    public void TheNewProfilesCarryNoSecret()
    {
        string[] secretWords = ["password", "passphrase", "secret", "token", "privatekey", "credential", "apikey", "bearer", "env"];
        foreach (var type in new[] { typeof(ContainerProfile), typeof(WslProfile), typeof(ContainerSummary), typeof(ContainerListing) })
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (var word in secretWords)
                {
                    property.Name.ShouldNotContain(word, Case.Insensitive, $"{type.Name}.{property.Name} looks secret-bearing");
                }
            }
        }
    }
}
