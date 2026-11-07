using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Presentation.Dispatch;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Dispatch;

/// <summary>The Dispatch presentation: one phase or message per state, plain task text, and the host shapes.</summary>
public sealed class DispatchViewModelTests
{
    /// <summary>Every phase, including the account-level ones, has its own non-empty text.</summary>
    [Fact]
    public void EveryPhaseHasItsOwnNonEmptyText()
    {
        var texts = Enum.GetValues<DispatchPhase>().Select(DispatchCopy.ForPhase).ToList();

        texts.ShouldAllBe(text => !string.IsNullOrWhiteSpace(text));
        texts.Distinct(StringComparer.Ordinal).Count().ShouldBe(texts.Count);
    }

    /// <summary>Every degraded state that is not a phase has its own non-empty message.</summary>
    [Fact]
    public void EveryDegradedStateHasItsOwnNonEmptyMessage()
    {
        var texts = Enum.GetValues<DispatchDegradation>().Select(DispatchCopy.ForDegradation).ToList();

        texts.ShouldAllBe(text => !string.IsNullOrWhiteSpace(text));
        texts.Distinct(StringComparer.Ordinal).Count().ShouldBe(texts.Count);
    }

    /// <summary>Every closed failure reason has its own non-empty message, so no failure shows a generic line.</summary>
    [Fact]
    public void EveryClosedFailureReasonHasItsOwnNonEmptyMessage()
    {
        var texts = Enum.GetValues<DispatchFailureReason>().Select(DispatchCopy.ForFailure).ToList();

        texts.ShouldAllBe(text => !string.IsNullOrWhiteSpace(text));
        texts.Distinct(StringComparer.Ordinal).Count().ShouldBe(texts.Count);
    }

    /// <summary>A 404 dispatch_disabled answer is the Disabled state, which is also what an owner-token or Demo deployment sees.</summary>
    [Fact]
    public void A404DispatchDisabledMapsToTheDisabledKind()
    {
        var failure = DispatchHostException.FromStatus(404, "dispatch_disabled", "Dispatch is off.");

        failure.Kind.ShouldBe(DispatchHostFailureKind.Disabled);
    }

    /// <summary>Task text is plain and capped at two hundred characters; markup is shown as the text it is.</summary>
    [Fact]
    public void TaskTextIsCappedAtTwoHundredCharactersAndNeverParsed()
    {
        var longText = new string('a', 250);

        DispatchText.Plain(longText).Length.ShouldBe(200);
        DispatchText.Plain("<b>bold</b>").ShouldBe("<b>bold</b>");
    }

    /// <summary>A host that is not configured refuses every call with the NotConfigured kind and never touches a network.</summary>
    [Fact]
    public async Task TheNotConfiguredHostRefusesEveryCallWithItsKind()
    {
        var host = new NotConfiguredDispatchHost();

        var failure = await Should.ThrowAsync<DispatchHostException>(() => host.ListDevicesAsync(CancellationToken.None)).ConfigureAwait(true);

        failure.Kind.ShouldBe(DispatchHostFailureKind.NotConfigured);
    }

    /// <summary>The fake host lists the devices it was given and records each rename it receives.</summary>
    [Fact]
    public async Task TheFakeHostListsItsDevicesAndRecordsARename()
    {
        var device = new DispatchDevice("device-1", "Office Mac", Online: true, PresenceKnown: true);
        var host = new FakeDispatchHost([device]);

        var devices = await host.ListDevicesAsync(CancellationToken.None).ConfigureAwait(true);
        await host.RenameDeviceAsync("device-1", "Desk", CancellationToken.None).ConfigureAwait(true);

        devices.ShouldBe([device]);
        host.Renames.ShouldBe([("device-1", "Desk")]);
    }

    /// <summary>An offline device shows the DeviceOffline phase with that phase's own text.</summary>
    [Fact]
    public void AnOfflineDeviceRowShowsTheDeviceOfflinePhase()
    {
        var row = DeviceRowModel.From(new DispatchDevice("device-1", "Office Mac", Online: false, PresenceKnown: true));

        row.Phase.ShouldBe(DispatchPhase.DeviceOffline);
        row.StatusText.ShouldBe(DispatchCopy.ForPhase(DispatchPhase.DeviceOffline));
    }

    /// <summary>A device whose presence is unknown, because Dragonfly is down, says so instead of claiming it is offline.</summary>
    [Fact]
    public void AUnknownPresenceRowSaysPresenceIsUnknown()
    {
        var row = DeviceRowModel.From(new DispatchDevice("device-1", "Office Mac", Online: false, PresenceKnown: false));

        row.Phase.ShouldBeNull();
        row.StatusText.ShouldBe(DispatchCopy.ForDegradation(DispatchDegradation.PresenceUnknown));
    }
}
