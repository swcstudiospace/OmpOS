using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Policy;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Policy;

/// <summary>D-06 order: the first failing step wins, and every denial carries a message id.</summary>
public sealed class GateOrderTests
{
    [Fact]
    public async Task DisabledBeatsNoGrant()
    {
        GateFixture fixture = await GateFixture.CreateAsync(enabled: false, signedIn: false).ConfigureAwait(true);
        ComputerGateDenied denied = await fixture.DenyAsync().ConfigureAwait(true);
        denied.Code.ShouldBe(DenialCode.Disabled);
        denied.UserMessageId.ShouldBe(DenialCodeCatalog.UserMessageId(DenialCode.Disabled));
    }

    [Fact]
    public async Task SignedOutIsDisabledAtStep1()
    {
        GateFixture fixture = await GateFixture.CreateAsync(signedIn: false).ConfigureAwait(true);
        (await fixture.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.Disabled);
    }

    [Fact]
    public async Task NonCoworkSessionIsDisabledAtStep1()
    {
        GateFixture fixture = await GateFixture.CreateAsync(cowork: false).ConfigureAwait(true);
        (await fixture.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.Disabled);
    }

    [Fact]
    public async Task NotOptedInBeatsPlatformUnavailable()
    {
        GateFixture fixture = await GateFixture.CreateAsync().ConfigureAwait(true);
        fixture.Backend.SetAvailability(ComputerAvailability.WaylandUnsupported);
        (await fixture.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.NotOptedIn);
    }

    [Fact]
    public async Task RevokedBeatsTierTooLow()
    {
        GateFixture fixture = await GateFixture.ReadyAsync(ComputerGrantTier.ViewOnly).ConfigureAwait(true);
        await fixture.KillSwitch.TripAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        fixture.Request = fixture.Request with { ToolName = "computer_type" };
        (await fixture.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.Revoked);
    }

    [Fact]
    public async Task PlatformUnavailableBeatsStaleFrame()
    {
        GateFixture fixture = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        fixture.Backend.SetAvailability(ComputerAvailability.NoDisplay);
        fixture.Request = fixture.Request with { FrameId = "stale" };
        (await fixture.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.PlatformUnavailable);
    }

    [Fact]
    public async Task PermissionMissingBeatsOutOfBounds()
    {
        GateFixture fixture = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        fixture.Backend.SetPermissions(
        [
            new ComputerPermissionStatus { Kind = ComputerPermissionKind.Accessibility, Granted = false, MessageId = "computer.permission.accessibility" },
        ]);
        fixture.Request = fixture.Request with { ScaledCoordinates = new ModelPoint { X = 9_000, Y = 0 } };
        (await fixture.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.PermissionMissing);
    }

    [Fact]
    public async Task KillSwitchUnavailableBeatsDeniedApplication()
    {
        GateFixture fixture = await GateFixture.CreateAsync().ConfigureAwait(true);
        fixture.Gate.OptIn("session");
        fixture.Backend.SetWindows([GateFixture.Window("target", "xterm", "shell", false), GateFixture.Indicator()]);
        (await fixture.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.KillSwitchUnavailable);
    }

    [Fact]
    public async Task IndicatorUnavailableBeatsForbiddenChord()
    {
        GateFixture fixture = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        fixture.Backend.SetWindows([GateFixture.Window("target", "code", "editor", false, minimizedIndicator: true)]);
        fixture.BindChord("Win+R");
        (await fixture.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.IndicatorUnavailable);
    }

    [Fact]
    public async Task StaleFrameBeatsForbiddenChord()
    {
        GateFixture fixture = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        fixture.Request = fixture.Request with { FrameId = "old" };
        fixture.BindChord("Win+R");
        (await fixture.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.StaleFrame);
    }

    [Fact]
    public async Task OutOfBoundsBeatsNoGrant()
    {
        GateFixture fixture = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        fixture.Request = fixture.Request with
        {
            ScaledCoordinates = new ModelPoint { X = -1, Y = 0 },
            Target = null,
        };
        fixture.Backend.SetWindows([GateFixture.Indicator()]);
        (await fixture.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.OutOfBounds);
    }

    [Fact]
    public async Task DeniedApplicationBeatsRateLimit()
    {
        GateFixture fixture = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        fixture.Backend.SetWindows([GateFixture.Window("target", "xterm", "shell", false), GateFixture.Indicator()]);
        fixture.ExhaustRate();
        (await fixture.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.DeniedApplication);
    }

    [Fact]
    public async Task OwnWindowBeatsApproval()
    {
        GateFixture fixture = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        fixture.Backend.SetWindows([GateFixture.Window("target", "code", "Phrase", true), GateFixture.Indicator()]);
        fixture.Grants.TryCreateFromUser("session", "code", ComputerGrantTier.Full, ComputerActionOrigin.Local, null, false, out _);
        fixture.Prompt.Calls.ShouldBe(0);
        (await fixture.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.OwnWindow);
        fixture.Prompt.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task NoGrantIsReported()
    {
        GateFixture fixture = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        fixture.Grants.EndSession("session");
        (await fixture.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.NoGrant);
    }

    [Fact]
    public async Task TierTooLowBeatsForbiddenChord()
    {
        GateFixture fixture = await GateFixture.ReadyAsync(ComputerGrantTier.ViewOnly).ConfigureAwait(true);
        fixture.BindChord("Win+R");
        fixture.Request = fixture.Request with { ToolName = "computer_chord" };
        (await fixture.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.TierTooLow);
    }

    [Fact]
    public async Task RateLimitBeatsApproval()
    {
        GateFixture fixture = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        fixture.ExhaustRate();
        (await fixture.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.RateLimited);
        fixture.Prompt.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task StepLimitAndTakeoverAndImageBudgetDeny()
    {
        GateFixture steps = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        steps.Limiter.ExhaustSteps();
        (await steps.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.StepLimit);

        GateFixture takeover = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        takeover.KillSwitch.NoteTakeover();
        (await takeover.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.UserTookOver);

        GateFixture images = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        for (int i = 0; i < images.Options.MaxImagesPerSession; i++)
        {
            images.Gate.RecordImage();
        }

        images.Request = images.Request with { ToolName = "computer_screenshot" };
        images.Bind(ComputerActionClass.Screenshot);
        (await images.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.ImageBudget);
    }

    [Fact]
    public async Task ApprovalDeniedTimeoutAndMissingApprover()
    {
        GateFixture denied = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        denied.Prompt.Choice = ComputerApprovalChoice.Deny;
        (await denied.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.ApprovalDenied);

        GateFixture timed = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        timed.Prompt.Choice = ComputerApprovalChoice.Deny;
        timed.Prompt.AdvanceOnCall = ComputerApprovalFacts.Timeout;
        (await timed.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.ApprovalTimeout);

        GateFixture missing = await GateFixture.ReadyAsync(ComputerGrantTier.Full, hasApprover: false).ConfigureAwait(true);
        (await missing.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.NoLocalApprover);
    }

    [Fact]
    public async Task AuditUnavailableAndInternalErrorAndElevatedAndSecureDesktop()
    {
        GateFixture audit = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        audit.Audit.Succeed = false;
        await audit.MarkSeenAsync().ConfigureAwait(true);
        audit.Grants.TryCreateFromUser("session", "code", ComputerGrantTier.Full, ComputerActionOrigin.Local, null, true, out _);
        (await audit.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.AuditUnavailable);

        GateFixture elevated = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        elevated.Backend.SetWindows([GateFixture.Window("target", "notepad|elevated", "admin", false), GateFixture.Indicator()]);
        elevated.Grants.TryCreateFromUser("session", "notepad|elevated", ComputerGrantTier.Full, ComputerActionOrigin.Local, null, true, out _);
        await elevated.MarkSeenAsync().ConfigureAwait(true);
        (await elevated.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.ElevatedTarget);

        GateFixture secure = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        secure.Backend.SetAvailability(ComputerAvailability.SecureDesktop);
        await secure.MarkSeenAsync().ConfigureAwait(true);
        secure.Grants.TryCreateFromUser("session", "code", ComputerGrantTier.Full, ComputerActionOrigin.Local, null, true, out _);
        (await secure.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.SecureDesktop);

        GateFixture broken = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        broken.Backend.FailListWindowsAfter(1);
        (await broken.DenyAsync().ConfigureAwait(true)).Code.ShouldBe(DenialCode.InternalError);
    }

    [Fact]
    public async Task AllowedDecisionCarriesATwoSecondTicket()
    {
        GateFixture fixture = await GateFixture.ReadyAsync(ComputerGrantTier.Full).ConfigureAwait(true);
        await fixture.MarkSeenAsync().ConfigureAwait(true);
        fixture.Grants.TryCreateFromUser("session", "code", ComputerGrantTier.Full, ComputerActionOrigin.Local, null, true, out _);
        ComputerGateDecision decision = await fixture.Gate.AuthorizeAsync(fixture.Request, TestContext.Current.CancellationToken).ConfigureAwait(true);
        ComputerGateAllowed allowed = decision.ShouldBeOfType<ComputerGateAllowed>();
        allowed.Ticket.IsWithinLifetime(fixture.Time.UtcNow).ShouldBeTrue();
        allowed.Ticket.TryConsume().ShouldBeTrue();
        allowed.Ticket.TryConsume().ShouldBeFalse();
    }
}
