using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Policy;
using Autonogrammer.Harness.ComputerUse.Tests.Policy;
using Autonogrammer.Harness.ComputerUse.Tools;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Tools;

/// <summary>Registration rule, overlay ownership and the absence of a relay operation.</summary>
public sealed class ComputerUseDispatchRegistrationTests
{
    [Fact]
    public void RegistersOnlyForCoworkComputerUseWithBothFlags()
    {
        Query(cowork: true, classes: ["computer_use"], enabled: true, dispatch: true).ShouldBeTrue();
        Query(cowork: true, classes: ["computer_use"], enabled: true, dispatch: true, codePrompt: true).ShouldBeFalse();
        Query(cowork: true, classes: [], enabled: true, dispatch: true).ShouldBeFalse();
        Query(cowork: false, classes: ["computer_use"], enabled: true, dispatch: true, codeMode: true).ShouldBeFalse();
        Query(cowork: false, classes: ["computer_use"], enabled: true, dispatch: true, api: true).ShouldBeFalse();
        Query(cowork: false, classes: ["computer_use"], enabled: true, dispatch: true, web: true).ShouldBeFalse();
        Query(cowork: false, classes: ["computer_use"], enabled: true, dispatch: true, cloud: true).ShouldBeFalse();
        Query(cowork: true, classes: ["computer_use"], enabled: false, dispatch: true).ShouldBeFalse();
    }

    [Fact]
    public void DispatchGrantsExpireWithinThirtyMinutesAndSourcesNameNoRelay()
    {
        ManualTimeProvider time = new(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));
        GrantStore grants = new(time);
        grants.TryCreateFromUser("s", "app", ComputerGrantTier.Full, ComputerActionOrigin.Dispatch, time.UtcNow.AddHours(2), true, out _).ShouldBeTrue();
        grants.Snapshot().Count.ShouldBe(1);
        time.Advance(TimeSpan.FromMinutes(30));
        grants.Snapshot().Count.ShouldBe(0);
        string root = FindRepo();
        string profile = File.ReadAllText(Path.Combine(root, "src/Autonogrammer.Cowork/Chat/CoworkSessionProfile.cs"));
        profile.ShouldContain("browser:\\n  enabled: false");
        profile.ShouldContain("computer:\\n  enabled: false");
        profile.Contains("EnvironmentMode", StringComparison.Ordinal).ShouldBeFalse();
        string source = string.Concat(Directory.GetFiles(Path.Combine(root, "src/Autonogrammer.Harness.ComputerUse"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        source.Contains("IAuthSession", StringComparison.Ordinal).ShouldBeFalse();
        source.Contains("IApiTokenProvider", StringComparison.Ordinal).ShouldBeFalse();
        source.Contains("\"computer.screenshot\"", StringComparison.Ordinal).ShouldBeFalse();
        source.Contains("\"computer.click\"", StringComparison.Ordinal).ShouldBeFalse();
        string tools = File.ReadAllText(Path.Combine(root, "src/Autonogrammer.Harness.ComputerUse/Tools/ComputerToolProvider.cs"));
        tools.Contains("InjectAsync", StringComparison.Ordinal).ShouldBeFalse();
        source.Contains("IAuthConfigSource", StringComparison.Ordinal).ShouldBeFalse();
        source.Contains("IToolApprovalSettings", StringComparison.Ordinal).ShouldBeFalse();
        source.Contains("EngineEnvironmentPolicy", StringComparison.Ordinal).ShouldBeFalse();
        source.Contains("enabled: false", StringComparison.Ordinal).ShouldBeFalse();
    }

    private static bool Query(bool cowork, IReadOnlyList<string> classes, bool enabled, bool dispatch, bool codePrompt = false, bool codeMode = false, bool api = false, bool web = false, bool cloud = false) =>
        ComputerToolProvider.ShouldRegisterFor(new ComputerRegistrationQuery
        {
            CoworkSurface = cowork,
            CodePrompt = codePrompt,
            CodeMode = codeMode,
            Api = api,
            Web = web,
            CloudWorker = cloud,
            ToolClasses = classes,
            Enabled = enabled,
            AllowDispatchSessions = dispatch,
        });

    private static string FindRepo()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src/Autonogrammer.Cowork/Chat/CoworkSessionProfile.cs")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
