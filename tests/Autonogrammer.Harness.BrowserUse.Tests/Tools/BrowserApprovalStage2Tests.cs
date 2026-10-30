using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.BrowserUse.Options;
using Autonogrammer.Harness.BrowserUse.Policy;
using Autonogrammer.Harness.BrowserUse.Tools;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.BrowserUse.Tests.Tools;

/// <summary>Stage-2 cards, the local port and beforeunload.</summary>
public sealed class BrowserApprovalStage2Tests
{
    [Fact]
    public async Task LocalCard_OpensThePortOnlyAfterTheAnswer_AndStoresTheGrant()
    {
        var ports = new RecordingPorts();
        var grants = new OriginGrants();
        var broker = new AnsweringBroker(ports);
        var stage = new BrowserApprovalStage2(broker, ports, grants);
        var allowed = await stage.AskRedirectAsync("s", "c", "browser_navigate", new Uri("http://127.0.0.1:9/app"), PolicyMode.PromptOnNewOrigin, CoworkTurnOrigin.Local, CancellationToken.None).ConfigureAwait(true);
        allowed.ShouldBeTrue();
        ports.Opened.ShouldBe(("127.0.0.1", 9));
        grants.Contains("127.0.0.1", 9).ShouldBeTrue();
        broker.SawClosedPort.ShouldBeTrue();
    }

    [Fact]
    public void BeforeUnload_IsAccepted_AndOtherDialogsAreDismissed()
    {
        BrowserApprovalStage2.AcceptDialog("beforeunload").ShouldBeTrue();
        BrowserApprovalStage2.AcceptDialog("alert").ShouldBeFalse();
        BrowserApprovalStage2.AcceptDialog("confirm").ShouldBeFalse();
    }

    private sealed class RecordingPorts : ILocalPortPermit
    {
        public (string Host, int Port) Opened { get; private set; }

        public int Opens { get; private set; }

        public void Open(string host, int port)
        {
            Opens++;
            Opened = (host, port);
        }
    }

    private sealed class AnsweringBroker : ICoworkApprovalBroker
    {
        private readonly RecordingPorts _ports;

        public AnsweringBroker(RecordingPorts ports) => _ports = ports;

        public bool SawClosedPort { get; private set; }

        public ValueTask<CoworkApprovalDecision> RequestAsync(CoworkApprovalRequest request, CancellationToken cancellationToken)
        {
            SawClosedPort = _ports.Opens == 0;
            request.Preview.Grant.ShouldBe(new CoworkGrantKey("browser.local", "127.0.0.1:9"));
            return ValueTask.FromResult(new CoworkApprovalDecision(CoworkApprovalOutcome.Allowed));
        }
    }
}
