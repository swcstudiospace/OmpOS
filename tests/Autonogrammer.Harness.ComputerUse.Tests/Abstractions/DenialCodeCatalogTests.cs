using Autonogrammer.Harness.ComputerUse.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Abstractions;

/// <summary>Denial catalogue, availability remediation ids, and the source contract of the abstractions project.</summary>
public sealed class DenialCodeCatalogTests
{
    [Fact]
    public void EveryDenialCodeHasADistinctMessageId()
    {
        DenialCode[] codes = Enum.GetValues<DenialCode>();
        string[] ids = codes.Select(DenialCodeCatalog.UserMessageId).ToArray();
        ids.ShouldAllBe(id => !string.IsNullOrWhiteSpace(id));
        ids.Distinct(StringComparer.Ordinal).Count().ShouldBe(codes.Length);
    }

    [Fact]
    public void GateStepOrderMatchesD06()
    {
        string order = DenialCodeCatalog.GateStepOrder;
        string[] expected =
        [
            "(1)",
            "Disabled",
            "NotOptedIn",
            "Revoked",
            "(2)",
            "PlatformUnavailable",
            "PermissionMissing",
            "KillSwitchUnavailable",
            "IndicatorUnavailable",
            "(3)",
            "StaleFrame",
            "OutOfBounds",
            "(4)",
            "ListWindows",
            "(5)",
            "DeniedApplication",
            "OwnWindow",
            "NoGrant",
            "TierTooLow",
            "ForbiddenChord",
            "ElevatedTarget",
            "SecureDesktop",
            "(6)",
            "RateLimited",
            "StepLimit",
            "UserTookOver",
            "ImageBudget",
            "(7)",
            "ApprovalDenied",
            "ApprovalTimeout",
            "NoLocalApprover",
            "(8)",
            "AuditUnavailable",
        ];
        int cursor = 0;
        foreach (string token in expected)
        {
            int found = order.IndexOf(token, cursor, StringComparison.Ordinal);
            found.ShouldBeGreaterThanOrEqualTo(0, token);
            cursor = found + token.Length;
        }
    }

    [Fact]
    public void EveryAvailabilityMapsToOneRemediationId()
    {
        ComputerAvailability[] values = Enum.GetValues<ComputerAvailability>();
        string[] ids = values.Select(ComputerAvailabilityRemediation.For).ToArray();
        ids.Distinct(StringComparer.Ordinal).Count().ShouldBe(values.Length);
        ComputerAvailabilityRemediation.For(ComputerAvailability.WaylandUnsupported).ShouldBe("computer.unavailable.wayland");
        ComputerAvailabilityRemediation.For(ComputerAvailability.UnsupportedPlatform).ShouldBe("computer.unavailable.platform");
    }

    [Fact]
    public void BackendIoMembersAreValueTaskAsyncWithCancellationTokenLast()
    {
        string root = FindRepoRoot();
        string abstractions = Path.Combine(root, "src", "Autonogrammer.Harness.ComputerUse.Abstractions");
        string[] files =
        [
            "IComputerUseBackend.cs",
            "IComputerActionGate.cs",
            "ComputerUsePorts.cs",
        ];
        int seen = 0;
        foreach (string file in files)
        {
            foreach (string line in File.ReadAllLines(Path.Combine(abstractions, file)))
            {
                string trimmed = line.Trim();
                if (!trimmed.Contains("ValueTask", StringComparison.Ordinal) || !trimmed.Contains('(', StringComparison.Ordinal))
                {
                    continue;
                }

                int paren = trimmed.IndexOf('(', StringComparison.Ordinal);
                string head = trimmed[..paren];
                int lastSpace = head.LastIndexOf(' ');
                string name = head[(lastSpace + 1)..];
                name.ShouldEndWith("Async");
                trimmed.ShouldContain("CancellationToken cancellationToken");
                trimmed.ShouldNotContain("= default");
                trimmed.TrimEnd(';').TrimEnd().ShouldEndWith("CancellationToken cancellationToken)");
                seen++;
            }
        }

        seen.ShouldBeGreaterThan(10);
    }

    [Fact]
    public void AbstractionsProjectHasNoProcessPackageOrProjectReference()
    {
        string root = FindRepoRoot();
        string abstractions = Path.Combine(root, "src", "Autonogrammer.Harness.ComputerUse.Abstractions");
        string csproj = File.ReadAllText(Path.Combine(abstractions, "Autonogrammer.Harness.ComputerUse.Abstractions.csproj"));
        csproj.ShouldNotContain("PackageReference");
        csproj.ShouldNotContain("ProjectReference");
        foreach (string file in Directory.GetFiles(abstractions, "*.cs"))
        {
            string text = File.ReadAllText(file);
            text.ShouldNotContain("System.Diagnostics.Process");
            text.ShouldNotContain("DllImport");
        }

        string backend = File.ReadAllText(Path.Combine(abstractions, "IComputerUseBackend.cs"));
        backend.ShouldNotContain("X11");
        backend.ShouldNotContain("HWND");
        backend.ShouldNotContain("CGEvent");
    }

    [Fact]
    public void PortsDoNotReferenceWebUiOrCoworkTypes()
    {
        string root = FindRepoRoot();
        string text = File.ReadAllText(Path.Combine(root, "src", "Autonogrammer.Harness.ComputerUse.Abstractions", "ComputerUsePorts.cs"));
        text.ShouldNotContain("using Autonogrammer.UI");
        text.ShouldNotContain("using Autonogrammer.Web");
        text.ShouldNotContain("using Autonogrammer.Cowork");
        text.ShouldNotContain("IAuthSession");
        text.ShouldNotContain("IApiTokenProvider");
        text.ShouldNotContain("OwnerTokenSession");
    }

    [Fact]
    public void CoordinateMapIsPure()
    {
        string root = FindRepoRoot();
        string text = File.ReadAllText(Path.Combine(root, "src", "Autonogrammer.Harness.ComputerUse.Abstractions", "CoordinateMap.cs"));
        text.ShouldNotContain("await");
        text.ShouldNotContain("DateTime");
        text.ShouldNotContain("DllImport");
    }

    [Fact]
    public void ValidatorAndReaderDoNotUseOptionsInfrastructure()
    {
        string root = FindRepoRoot();
        string abstractions = Path.Combine(root, "src", "Autonogrammer.Harness.ComputerUse.Abstractions");
        foreach (string name in new[] { "ComputerUseOptionsValidator.cs", "ComputerUseOptionsReader.cs" })
        {
            string text = File.ReadAllText(Path.Combine(abstractions, name));
            text.ShouldNotContain("IValidateOptions");
            text.ShouldNotContain("IOptions");
            text.ShouldNotContain("IConfiguration");
            text.ShouldNotContain("IServiceCollection");
        }

        string json = File.ReadAllText(Path.Combine(abstractions, "ComputerUseJsonContext.cs"));
        json.ShouldContain("JsonSerializerContext");
        foreach (string file in Directory.GetFiles(abstractions, "*.cs"))
        {
            string text = File.ReadAllText(file);
            text.ShouldNotContain("JsonSerializer.Serialize");
            text.ShouldNotContain("JsonSerializer.Deserialize");
        }
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Autonogrammer.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root was not found from the test output directory.");
    }
}
