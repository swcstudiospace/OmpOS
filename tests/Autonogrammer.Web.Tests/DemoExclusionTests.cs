using System.Reflection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>A production web head holds no Demo code: the assembly under test says whether it is a Demo build, and the Demo types exist exactly when it is.</summary>
public sealed class DemoExclusionTests
{
    private const string DemoHandlerTypeName = "Autonogrammer.Web.Demo.DemoApiHandler";

    /// <summary>The web head assembly carries <c>AutonogrammerDemo</c> metadata that matches whether it contains the Demo handler.</summary>
    [Fact]
    public void DemoTypesExistInTheWebHeadOnlyInADemoBuild()
    {
        var web = typeof(Autonogrammer.Web.Services.WebWorkbench).Assembly;
        var flag = web.GetCustomAttributes<AssemblyMetadataAttribute>().Single(static attribute => string.Equals(attribute.Key, "AutonogrammerDemo", StringComparison.Ordinal)).Value;

        flag.ShouldBeOneOf("true", "false");
        var present = web.GetType(DemoHandlerTypeName) is not null;
        present.ShouldBe(string.Equals(flag, "true", StringComparison.Ordinal), "the Demo handler is compiled into the web head if and only if AutonogrammerDemo=true");
    }

    /// <summary>The Demo sources compiled into this test project are the web head's own files, not a copy: the type lives in this assembly under the same name.</summary>
    [Fact]
    public void TestsCompileTheDemoSourcesThemselves()
    {
        var testAssembly = typeof(DemoExclusionTests).Assembly;

        var inTests = testAssembly.GetType(DemoHandlerTypeName) is not null;
        var inWeb = typeof(Autonogrammer.Web.Services.WebWorkbench).Assembly.GetType(DemoHandlerTypeName) is not null;

        (inTests || inWeb).ShouldBeTrue("the Demo handler must be reachable by the tests");
        (inTests && inWeb).ShouldBeFalse("a production-shaped web head must not also carry the Demo types");
    }
}
