using OmpOS.Native.Agents;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.AgentLoop;

/// <summary>
/// Where the head finds its plugins, its state and the Substrate: the precedence of the variables over the searches, the refusal of a
/// Substrate address that would send a token in the clear, and the guarantee that no token is kept by the environment object.
/// </summary>
public sealed class AgentLoopEnvironmentTests
{
    private const string Secret = "tok-9f3a-never-print-me";

    private static Func<string, string?> Variables(params (string Name, string Value)[] values)
    {
        var map = values.ToDictionary(static pair => pair.Name, static pair => pair.Value, StringComparer.Ordinal);
        return name => map.GetValueOrDefault(name);
    }

    private static AgentLoopEnvironment Resolve(Func<string, string?> variables, string baseDirectory, string currentDirectory, string home = "/home/someone") =>
        AgentLoopEnvironment.Resolve(variables, baseDirectory, currentDirectory, home);

    /// <summary>The plugins-root variable names the root, whatever repository the application sits in.</summary>
    [Fact]
    public void ThePluginsRootVariableOverridesTheRepositorySearch()
    {
        using var tree = new TempTree();
        tree.Write("repo/.gitmodules", string.Empty);
        var configured = Path.Combine(tree.Root, "elsewhere", "plugins");

        var environment = Resolve(
            Variables((AgentLoopEnvironment.PluginsRootVariable, configured)),
            Path.Combine(tree.Root, "repo"),
            tree.Workspace);

        environment.PluginsRoot.ShouldBe(configured);
    }

    /// <summary>A relative plugins-root variable is resolved against the current directory, not against the process.</summary>
    [Fact]
    public void ARelativePluginsRootIsResolvedAgainstTheCurrentDirectory()
    {
        using var tree = new TempTree();

        var environment = Resolve(Variables((AgentLoopEnvironment.PluginsRootVariable, "vendor/plugins")), tree.Root, tree.Workspace);

        environment.PluginsRoot.ShouldBe(Path.Combine(tree.Workspace, "vendor", "plugins"));
    }

    /// <summary>Without the variable, the root is the <c>plugins</c> directory of the nearest ancestor of the application that holds <c>.gitmodules</c>.</summary>
    [Fact]
    public void TheSearchStartsAtTheApplicationDirectoryAndWalksUp()
    {
        using var tree = new TempTree();
        tree.Write("repo/.gitmodules", string.Empty);
        Directory.CreateDirectory(Path.Combine(tree.Root, "repo", "apps", "OmpOS.Native", "bin"));

        var environment = Resolve(Variables(), Path.Combine(tree.Root, "repo", "apps", "OmpOS.Native", "bin"), tree.Workspace);

        environment.PluginsRoot.ShouldBe(Path.Combine(tree.Root, "repo", "plugins"));
    }

    /// <summary>When the application sits outside any repository, the search continues from the current directory.</summary>
    [Fact]
    public void TheSearchContinuesFromTheCurrentDirectory()
    {
        using var tree = new TempTree();
        tree.Write("repo/.gitmodules", string.Empty);
        var outside = Directory.CreateDirectory(Path.Combine(tree.Root, "installed", "bin")).FullName;
        var inside = Directory.CreateDirectory(Path.Combine(tree.Root, "repo", "src")).FullName;

        var environment = Resolve(Variables(), outside, inside);

        environment.PluginsRoot.ShouldBe(Path.Combine(tree.Root, "repo", "plugins"));
    }

    /// <summary>With no repository to be found, the root is a <c>plugins</c> directory below the current directory, which the plugin stages report as missing.</summary>
    [Fact]
    public void WithoutARepositoryTheRootIsBelowTheCurrentDirectory()
    {
        using var tree = new TempTree();
        var outside = Directory.CreateDirectory(Path.Combine(tree.Root, "installed")).FullName;

        var environment = Resolve(Variables(), outside, tree.Workspace);

        environment.PluginsRoot.ShouldBe(Path.Combine(tree.Workspace, "plugins"));
        Directory.Exists(environment.PluginsRoot).ShouldBeFalse();
    }

    /// <summary>The workspace root is the current directory.</summary>
    [Fact]
    public void TheWorkspaceRootIsTheCurrentDirectory()
    {
        using var tree = new TempTree();

        Resolve(Variables(), tree.Root, tree.Workspace).WorkspaceRoot.ShouldBe(tree.Workspace);
    }

    /// <summary>The state directory variable wins over the data home, which wins over the user's home.</summary>
    [Fact]
    public void TheStateRootFollowsTheVariableThenTheDataHomeThenTheHome()
    {
        using var tree = new TempTree();
        var explicitRoot = Path.Combine(tree.Root, "explicit-state");
        var dataHome = Path.Combine(tree.Root, "data-home");

        Resolve(Variables((AgentLoopEnvironment.StateDirectoryVariable, explicitRoot), ("XDG_DATA_HOME", dataHome)), tree.Root, tree.Workspace)
            .StateRoot.ShouldBe(explicitRoot);
        Resolve(Variables(("XDG_DATA_HOME", dataHome)), tree.Root, tree.Workspace)
            .StateRoot.ShouldBe(Path.Combine(dataHome, "OmpOS"));
        Resolve(Variables(), tree.Root, tree.Workspace, home: "/home/someone")
            .StateRoot.ShouldBe(Path.Combine("/home/someone", ".local", "share", "OmpOS"));
    }

    /// <summary>A data home that is not an absolute path is ignored, as the XDG base directory specification requires.</summary>
    [Fact]
    public void ARelativeDataHomeIsIgnored()
    {
        using var tree = new TempTree();

        Resolve(Variables(("XDG_DATA_HOME", "relative/data")), tree.Root, tree.Workspace, home: "/home/someone")
            .StateRoot.ShouldBe(Path.Combine("/home/someone", ".local", "share", "OmpOS"));
    }

    /// <summary>With no address, the Substrate is disabled and nothing is reported as wrong.</summary>
    [Fact]
    public void WithoutAnAddressTheSubstrateIsDisabled()
    {
        using var tree = new TempTree();

        var environment = Resolve(Variables(), tree.Root, tree.Workspace);

        environment.Substrate.ShouldBeNull();
        environment.SubstrateProblem.ShouldBeNull();
    }

    /// <summary>An https address and an http address on a loopback host are accepted.</summary>
    [Theory]
    [InlineData("https://substrate.example.test")]
    [InlineData("http://127.0.0.1:7410")]
    [InlineData("http://localhost:7410")]
    public void HttpsAndLoopbackHttpAddressesAreAccepted(string address)
    {
        using var tree = new TempTree();

        var environment = Resolve(Variables((AgentLoopEnvironment.SubstrateUrlVariable, address)), tree.Root, tree.Workspace);

        environment.Substrate.ShouldNotBeNull();
        environment.SubstrateProblem.ShouldBeNull();
    }

    /// <summary>Plain http to a host that is not loopback is refused: the bearer token would cross the network in the clear. The Substrate is then disabled and the reason is reported without the address.</summary>
    [Theory]
    [InlineData("http://substrate.example.test:7410")]
    [InlineData("http://10.0.0.5:7410")]
    [InlineData("https://user:hunter2@substrate.example.test")]
    [InlineData("ftp://substrate.example.test")]
    [InlineData("not an address")]
    public void AnAddressThatCouldLeakACredentialIsRefused(string address)
    {
        using var tree = new TempTree();

        var environment = Resolve(Variables((AgentLoopEnvironment.SubstrateUrlVariable, address)), tree.Root, tree.Workspace);

        environment.Substrate.ShouldBeNull();
        var problem = environment.SubstrateProblem.ShouldNotBeNull();
        problem.ShouldContain(AgentLoopEnvironment.SubstrateUrlVariable);
        problem.ShouldNotContain("hunter2");
        problem.ShouldNotContain("substrate.example.test");
        problem.ShouldNotContain("10.0.0.5");
    }

    /// <summary>The token comes from the default variable, or from the variable the operator names.</summary>
    [Fact]
    public void TheTokenComesFromTheDefaultOrTheNamedVariable()
    {
        using var tree = new TempTree();

        var byDefault = Resolve(Variables((AgentLoopEnvironment.DefaultSubstrateTokenVariable, Secret)), tree.Root, tree.Workspace);
        var named = Resolve(
            Variables((AgentLoopEnvironment.SubstrateTokenVariableNameVariable, "MY_SUBSTRATE_KEY"), ("MY_SUBSTRATE_KEY", Secret), (AgentLoopEnvironment.DefaultSubstrateTokenVariable, "not-this-one")),
            tree.Root,
            tree.Workspace);

        byDefault.SubstrateTokenVariable.ShouldBe("SUBSTRATE_TOKEN_OMP");
        byDefault.ReadSubstrateToken().ShouldBe(Secret);
        named.SubstrateTokenVariable.ShouldBe("MY_SUBSTRATE_KEY");
        named.ReadSubstrateToken().ShouldBe(Secret);
    }

    /// <summary>A variable name that is not a name (it could be a value pasted by mistake) is not used: the default variable is read instead.</summary>
    [Fact]
    public void ANameThatIsNotAVariableNameFallsBackToTheDefault()
    {
        using var tree = new TempTree();

        var environment = Resolve(
            Variables((AgentLoopEnvironment.SubstrateTokenVariableNameVariable, Secret + "=oops"), (AgentLoopEnvironment.DefaultSubstrateTokenVariable, "default-token")),
            tree.Root,
            tree.Workspace);

        environment.SubstrateTokenVariable.ShouldBe("SUBSTRATE_TOKEN_OMP");
        environment.ReadSubstrateToken().ShouldBe("default-token");
    }

    /// <summary>With no token in the variable, the first line of the token file is used; the variable wins when both exist.</summary>
    [Fact]
    public void TheTokenFileIsTheFallbackAndTheVariableWins()
    {
        using var tree = new TempTree();
        var file = tree.Write("token.txt", "\n  " + Secret + "  \nsecond line\n");

        var fromFile = Resolve(Variables((AgentLoopEnvironment.SubstrateTokenFileVariable, file)), tree.Root, tree.Workspace);
        var both = Resolve(
            Variables((AgentLoopEnvironment.SubstrateTokenFileVariable, file), (AgentLoopEnvironment.DefaultSubstrateTokenVariable, "from-variable")),
            tree.Root,
            tree.Workspace);

        fromFile.ReadSubstrateToken().ShouldBe(Secret);
        both.ReadSubstrateToken().ShouldBe("from-variable");
    }

    /// <summary>A token file that is missing or empty yields no token, and never an exception.</summary>
    [Fact]
    public void AMissingOrEmptyTokenFileYieldsNoToken()
    {
        using var tree = new TempTree();
        var empty = tree.Write("empty.txt", "  \n\n");

        Resolve(Variables((AgentLoopEnvironment.SubstrateTokenFileVariable, Path.Combine(tree.Root, "missing.txt"))), tree.Root, tree.Workspace)
            .ReadSubstrateToken().ShouldBeNull();
        Resolve(Variables((AgentLoopEnvironment.SubstrateTokenFileVariable, empty)), tree.Root, tree.Workspace)
            .ReadSubstrateToken().ShouldBeNull();
        Resolve(Variables(), tree.Root, tree.Workspace).ReadSubstrateToken().ShouldBeNull();
    }

    /// <summary>The token is read when it is asked for, so a token that changes between two reads is seen, and no field of the object holds it.</summary>
    [Fact]
    public void TheTokenIsReadOnDemandAndNeverKept()
    {
        using var tree = new TempTree();
        var current = Secret;
        var environment = Resolve(name => string.Equals(name, AgentLoopEnvironment.DefaultSubstrateTokenVariable, StringComparison.Ordinal) ? current : null, tree.Root, tree.Workspace);

        environment.ReadSubstrateToken().ShouldBe(Secret);
        current = "rotated-token";

        environment.ReadSubstrateToken().ShouldBe("rotated-token");
    }

    /// <summary>The description names locations and variables, never a token, an address with credentials or the content of the token file.</summary>
    [Fact]
    public void ToStringCarriesNoSecret()
    {
        using var tree = new TempTree();
        var file = tree.Write("token.txt", Secret);
        var environment = Resolve(
            Variables(
                (AgentLoopEnvironment.DefaultSubstrateTokenVariable, Secret),
                (AgentLoopEnvironment.SubstrateTokenFileVariable, file),
                (AgentLoopEnvironment.SubstrateUrlVariable, "https://substrate.example.test/private/path")),
            tree.Root,
            tree.Workspace);

        var text = environment.ToString();

        text.ShouldNotContain(Secret);
        text.ShouldNotContain("private/path");
        text.ShouldContain("https://substrate.example.test");
        text.ShouldContain("SUBSTRATE_TOKEN_OMP");
    }
}
