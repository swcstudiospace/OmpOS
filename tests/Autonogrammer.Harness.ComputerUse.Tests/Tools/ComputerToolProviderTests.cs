using System.Text.Json;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Fakes;
using Autonogrammer.Harness.ComputerUse.Tools;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Tools;

/// <summary>Fifteen descriptors, host-observed preview and untrusted screenshot envelopes.</summary>
public sealed class ComputerToolProviderTests
{
    private static readonly string[] Expected =
    [
        "computer_status",
        "computer_list_displays",
        "computer_list_windows",
        "computer_screenshot",
        "computer_zoom",
        "computer_request_access",
        "computer_mouse_move",
        "computer_click",
        "computer_drag",
        "computer_scroll",
        "computer_type",
        "computer_key",
        "computer_clipboard_read",
        "computer_clipboard_write",
        "computer_wait",
    ];

    [Fact]
    public async Task DescriptorsAndPreviewMatchTheContract()
    {
        RecordingBroker broker = new();
        ComputerToolProvider provider = Provider(broker);
        provider.ProviderId.ShouldBe("computer");
        IReadOnlyList<CoworkToolDescriptor> tools = provider.Describe();
        tools.Count.ShouldBe(15);
        tools.Select(static tool => tool.Name).ShouldBe(Expected);
        tools.ShouldAllBe(static tool => tool.Effect != CoworkToolEffect.Destructive);
        foreach (CoworkToolDescriptor tool in tools)
        {
            tool.Name[0].ShouldBeInRange('a', 'z');
            tool.Name.Contains("__", StringComparison.Ordinal).ShouldBeFalse();
            tool.Name.Length.ShouldBeInRange(3, 40);
            tool.Description.ShouldContain(ComputerToolProvider.OrderingText);
            tool.ProviderId.ShouldBe("computer");
        }

        Descriptor("computer_screenshot").Approval.ShouldBe(CoworkApprovalRequirement.ByPolicy);
        Descriptor("computer_screenshot").Effect.ShouldBe(CoworkToolEffect.ReadOnly);
        Descriptor("computer_screenshot").Results.ShouldBe(CoworkResultKinds.Text | CoworkResultKinds.Image);
        Descriptor("computer_request_access").Approval.ShouldBe(CoworkApprovalRequirement.EveryCallOnExecutingDevice);
        Descriptor("computer_click").Approval.ShouldBe(CoworkApprovalRequirement.ByProviderGate);
        CoworkCallPreview preview = await provider.PreviewAsync(Session(), Invocation("computer_click"), CancellationToken.None);
        preview.Grant.ShouldBe(new CoworkGrantKey("computer.app", "notes.app"));
        CoworkToolResult denied = await provider.InvokeAsync(Session(), Invocation("computer_click"), CancellationToken.None);
        denied.IsError.ShouldBeTrue();
        broker.MustBeLocal.ShouldBeTrue();
    }

    private static CoworkToolDescriptor Descriptor(string name) =>
        Provider(new RecordingBroker()).Describe().Single(tool => string.Equals(tool.Name, name, StringComparison.Ordinal));

    private static ComputerToolProvider Provider(RecordingBroker broker)
    {
        FakeComputerUseBackend backend = new();
        backend.SetWindows(
        [
            new WindowInfo
            {
                Id = "w",
                Pid = 4,
                AppIdentity = "notes.app",
                Title = "Notes",
                Bounds = new PixelRect { X = 0, Y = 0, Width = 10, Height = 10 },
                Minimized = false,
                Focused = true,
                IsOwnProcess = false,
            },
        ]);
        return new ComputerToolProvider(backend, null!, null!, null!, broker, new Access());
    }

    private static CoworkSessionContext Session() => new()
    {
        SessionId = "s",
        WorkingDirectory = "/",
        TrustedFolders = [],
        ModelReadsImages = true,
    };

    private static CoworkToolInvocation Invocation(string name) =>
        new("s", "c", name, JsonDocument.Parse("{}").RootElement, CoworkTurnOrigin.Local);

    private sealed class Access : IComputerUseAccessFacts
    {
        public bool IsSignedIn => true;

        public bool IsCoworkSession(string sessionId) => true;
    }

    private sealed class RecordingBroker : ICoworkApprovalBroker
    {
        public bool MustBeLocal { get; private set; }

        public ValueTask<CoworkApprovalDecision> RequestAsync(CoworkApprovalRequest request, CancellationToken cancellationToken)
        {
            MustBeLocal = request.MustBeLocal;
            return ValueTask.FromResult(new CoworkApprovalDecision(CoworkApprovalOutcome.Denied));
        }
    }
}
