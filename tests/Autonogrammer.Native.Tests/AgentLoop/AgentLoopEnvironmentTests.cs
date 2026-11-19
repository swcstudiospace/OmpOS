using Autonogrammer.Native.Agents;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.AgentLoop;

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
        Directory.CreateDirectory(Path.Combine(tree.Root, "repo", "apps", "Autonogrammer.Native", "bin"));

        var environment = Resolve(Variables(), Path.Combine(tree.Root, "repo", "apps", "Autonogrammer.Native", "bin"), tree.Workspace);

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

    /// <summary>A packaged install ships the plugins beside the executable: with no repository above the application, that <c>plugins</c> directory is the root.</summary>
    [Fact]
    public void APluginsDirectoryBesideTheApplicationIsTheRootWithNoRepository()
    {
        using var tree = new TempTree();
        var application = Directory.CreateDirectory(Path.Combine(tree.Root, "installed", "bin")).FullName;
        var beside = Directory.CreateDirectory(Path.Combine(application, "plugins")).FullName;

        var environment = Resolve(Variables(), application, tree.Workspace);

        environment.PluginsRoot.ShouldBe(beside);
    }

    /// <summary>The application's own plugins directory is taken before the repository search, so a packaged install inside a checkout uses its own plugins.</summary>
    [Fact]
    public void TheApplicationsPluginsDirectoryIsTakenBeforeTheRepositorySearch()
    {
        using var tree = new TempTree();
        tree.Write("repo/.gitmodules", string.Empty);
        var application = Directory.CreateDirectory(Path.Combine(tree.Root, "repo", "bin")).FullName;
        var beside = Directory.CreateDirectory(Path.Combine(application, "plugins")).FullName;

        var environment = Resolve(Variables(), application, tree.Workspace);

        environment.PluginsRoot.ShouldBe(beside);
    }

    /// <summary>A directory without a plugins directory is not a plugins root, and a file named <c>plugins</c> is not one either: the search continues.</summary>
    [Fact]
    public void AnApplicationDirectoryWithoutAPluginsDirectoryFallsThroughToTheSearch()
    {
        using var tree = new TempTree();
        tree.Write("repo/.gitmodules", string.Empty);
        var empty = Directory.CreateDirectory(Path.Combine(tree.Root, "repo", "empty")).FullName;
        var application = Directory.CreateDirectory(Path.Combine(tree.Root, "repo", "bin")).FullName;
        tree.Write("repo/bin/plugins", "a file, not a directory");

        Resolve(Variables(), empty, tree.Workspace).PluginsRoot.ShouldBe(Path.Combine(tree.Root, "repo", "plugins"));
        Resolve(Variables(), application, tree.Workspace).PluginsRoot.ShouldBe(Path.Combine(tree.Root, "repo", "plugins"));
    }

    /// <summary>The plugins-root variable still wins over a plugins directory beside the application.</summary>
    [Fact]
    public void ThePluginsRootVariableStillWinsOverThePluginsDirectoryBesideTheApplication()
    {
        using var tree = new TempTree();
        var application = Directory.CreateDirectory(Path.Combine(tree.Root, "installed", "bin")).FullName;
        Directory.CreateDirectory(Path.Combine(application, "plugins"));
        var configured = Path.Combine(tree.Root, "elsewhere", "plugins");

        var environment = Resolve(Variables((AgentLoopEnvironment.PluginsRootVariable, configured)), application, tree.Workspace);

        environment.PluginsRoot.ShouldBe(configured);
    }

    private static (string Executable, string Resources) MacBundle(TempTree tree, string parent = "")
    {
        var contents = Path.Combine(tree.Root, parent, "Autonogrammer.app", "Contents");
        var executable = Directory.CreateDirectory(Path.Combine(contents, "MacOS")).FullName;
        return (executable, Path.Combine(contents, "Resources", "plugins"));
    }

    /// <summary>A macOS app bundle keeps its plugins in <c>Contents/Resources/plugins</c>; the root is found from <c>Contents/MacOS</c> with or without a trailing separator.</summary>
    [Fact]
    public void AMacBundlesResourcesPluginsDirectoryIsTheRoot()
    {
        using var tree = new TempTree();
        var (executable, resources) = MacBundle(tree);
        Directory.CreateDirectory(resources);

        Resolve(Variables(), executable, tree.Workspace).PluginsRoot.ShouldBe(resources);
        Resolve(Variables(), executable + Path.DirectorySeparatorChar, tree.Workspace).PluginsRoot.ShouldBe(resources);
    }

    /// <summary>A plugins directory beside the executable wins over the bundle's <c>Resources/plugins</c>.</summary>
    [Fact]
    public void APluginsDirectoryBesideTheExecutableWinsOverTheBundleResources()
    {
        using var tree = new TempTree();
        var (executable, resources) = MacBundle(tree);
        Directory.CreateDirectory(resources);
        var beside = Directory.CreateDirectory(Path.Combine(executable, "plugins")).FullName;

        Resolve(Variables(), executable, tree.Workspace).PluginsRoot.ShouldBe(beside);
    }

    /// <summary>The plugins-root variable wins over both the directory beside the executable and the bundle's <c>Resources/plugins</c>.</summary>
    [Fact]
    public void ThePluginsRootVariableWinsOverTheBundleResources()
    {
        using var tree = new TempTree();
        var (executable, resources) = MacBundle(tree);
        Directory.CreateDirectory(resources);
        Directory.CreateDirectory(Path.Combine(executable, "plugins"));
        var configured = Path.Combine(tree.Root, "elsewhere", "plugins");

        var environment = Resolve(Variables((AgentLoopEnvironment.PluginsRootVariable, configured)), executable, tree.Workspace);

        environment.PluginsRoot.ShouldBe(configured);
    }

    /// <summary>Only the exact <c>Contents/MacOS</c> shape counts: a sibling <c>Resources/plugins</c> is ignored for any other base directory.</summary>
    [Fact]
    public void ABaseDirectoryThatIsNotContentsMacOsIgnoresASiblingResourcesPlugins()
    {
        using var tree = new TempTree();
        var contents = Path.Combine(tree.Root, "Autonogrammer.app", "Contents");
        Directory.CreateDirectory(Path.Combine(contents, "Resources", "plugins"));
        var other = Directory.CreateDirectory(Path.Combine(contents, "Other")).FullName;
        var lowerCase = Directory.CreateDirectory(Path.Combine(contents, "macos")).FullName;
        var withoutContents = Directory.CreateDirectory(Path.Combine(tree.Root, "Autonogrammer.app", "Frameworks", "MacOS")).FullName;
        Directory.CreateDirectory(Path.Combine(tree.Root, "Autonogrammer.app", "Frameworks", "Resources", "plugins"));
        var expected = Path.Combine(tree.Workspace, "plugins");

        Resolve(Variables(), other, tree.Workspace).PluginsRoot.ShouldBe(expected);
        Resolve(Variables(), lowerCase, tree.Workspace).PluginsRoot.ShouldBe(expected);
        Resolve(Variables(), withoutContents, tree.Workspace).PluginsRoot.ShouldBe(expected);
        Resolve(Variables(), withoutContents + Path.DirectorySeparatorChar, tree.Workspace).PluginsRoot.ShouldBe(expected);
    }

    /// <summary>The bundle shape without an existing <c>Resources/plugins</c> falls through to the repository search, and then to the workspace fallback, exactly as before.</summary>
    [Fact]
    public void ABundleWithoutResourcesPluginsFallsThroughToTheSearchAndTheWorkspace()
    {
        using var tree = new TempTree();
        var (bare, _) = MacBundle(tree);
        tree.Write("repo/.gitmodules", string.Empty);
        var (inRepository, _) = MacBundle(tree, "repo");

        Resolve(Variables(), bare, tree.Workspace).PluginsRoot.ShouldBe(Path.Combine(tree.Workspace, "plugins"));
        Resolve(Variables(), inRepository, tree.Workspace).PluginsRoot.ShouldBe(Path.Combine(tree.Root, "repo", "plugins"));
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
            .StateRoot.ShouldBe(Path.Combine(dataHome, "Autonogrammer"));
        Resolve(Variables(), tree.Root, tree.Workspace, home: "/home/someone")
            .StateRoot.ShouldBe(Path.Combine("/home/someone", ".local", "share", "Autonogrammer"));
    }

    /// <summary>A data home that is not an absolute path is ignored, as the XDG base directory specification requires.</summary>
    [Fact]
    public void ARelativeDataHomeIsIgnored()
    {
        using var tree = new TempTree();

        Resolve(Variables(("XDG_DATA_HOME", "relative/data")), tree.Root, tree.Workspace, home: "/home/someone")
            .StateRoot.ShouldBe(Path.Combine("/home/someone", ".local", "share", "Autonogrammer"));
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
