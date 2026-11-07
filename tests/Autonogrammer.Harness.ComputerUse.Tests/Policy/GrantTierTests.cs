using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Policy;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Policy;

/// <summary>Grant tiers, keys, expiry and the Dispatch ceiling.</summary>
public sealed class GrantTierTests
{
    [Fact]
    public void TierOrderIsViewThenClickThenFull()
    {
        ((int)ComputerGrantTier.ViewOnly).ShouldBeLessThan((int)ComputerGrantTier.ClickOnly);
        ((int)ComputerGrantTier.ClickOnly).ShouldBeLessThan((int)ComputerGrantTier.Full);
    }

    [Fact]
    public void EachTierAllowsOnlyItsActionClasses()
    {
        ComputerActionClass[] view = [ComputerActionClass.Screenshot, ComputerActionClass.Zoom, ComputerActionClass.ListWindows];
        ComputerActionClass[] pointer = [ComputerActionClass.PointerMove, ComputerActionClass.Click, ComputerActionClass.Drag, ComputerActionClass.Scroll];
        ComputerActionClass[] full = [ComputerActionClass.Type, ComputerActionClass.Chord, ComputerActionClass.Clipboard];
        foreach (ComputerActionClass action in view)
        {
            ComputerGrantPolicy.Allows(ComputerGrantTier.ViewOnly, action).ShouldBeTrue();
        }

        foreach (ComputerActionClass action in pointer.Concat(full))
        {
            ComputerGrantPolicy.Allows(ComputerGrantTier.ViewOnly, action).ShouldBeFalse();
        }

        foreach (ComputerActionClass action in view.Concat(pointer))
        {
            ComputerGrantPolicy.Allows(ComputerGrantTier.ClickOnly, action).ShouldBeTrue();
        }

        foreach (ComputerActionClass action in full)
        {
            ComputerGrantPolicy.Allows(ComputerGrantTier.ClickOnly, action).ShouldBeFalse();
        }

        foreach (ComputerActionClass action in view.Concat(pointer).Concat(full))
        {
            ComputerGrantPolicy.Allows(ComputerGrantTier.Full, action).ShouldBeTrue();
        }
    }

    [Fact]
    public void GrantKeyCarriesSessionAppTierOriginExpiryAndTyping()
    {
        ManualTimeProvider time = new(DateTimeOffset.Parse("2026-10-30T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        GrantStore store = new(time);
        store.TryCreateFromUser("session", "code", ComputerGrantTier.ClickOnly, ComputerActionOrigin.Local, time.UtcNow.AddHours(1), true, out ComputerGrant grant).ShouldBeTrue();
        grant.SessionId.ShouldBe("session");
        grant.AppIdentity.ShouldBe("code");
        grant.Tier.ShouldBe(ComputerGrantTier.ClickOnly);
        grant.Origin.ShouldBe(ComputerActionOrigin.Local);
        grant.Expiry.ShouldBe(time.UtcNow.AddHours(1));
        grant.AutoApproveTyping.ShouldBeTrue();
    }

    [Fact]
    public void ModelCreatedGrantIsRefused()
    {
        GrantStore store = new(new ManualTimeProvider(DateTimeOffset.UnixEpoch));
        store.TryCreateFromModel().ShouldBeFalse();
        store.Snapshot().ShouldBeEmpty();
    }

    [Fact]
    public void GrantEndsWithSessionAfterEightHoursAndOnWindowSetChange()
    {
        ManualTimeProvider time = new(DateTimeOffset.Parse("2026-10-30T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        GrantStore store = new(time);
        store.TryCreateFromUser("session", "code", ComputerGrantTier.Full, ComputerActionOrigin.Local, time.UtcNow.AddDays(2), false, out ComputerGrant capped);
        capped.Expiry.ShouldBe(time.UtcNow.AddHours(8));

        store.EndSession("session");
        store.Snapshot().ShouldBeEmpty();

        store.TryCreateFromUser("session", "code", ComputerGrantTier.Full, ComputerActionOrigin.Local, null, false, out _);
        store.ObserveWindowIdentities("session", ["code"]);
        store.Snapshot().ShouldNotBeEmpty();
        store.ObserveWindowIdentities("session", ["code", "firefox"]);
        store.Snapshot().ShouldBeEmpty();

        store.TryCreateFromUser("session", "code", ComputerGrantTier.Full, ComputerActionOrigin.Local, null, false, out _);
        time.Advance(TimeSpan.FromHours(8));
        store.Snapshot().ShouldBeEmpty();
    }

    [Fact]
    public void DispatchGrantExpiresAtCommandEndOrThirtyMinutesAndNeverAutoApprovesTyping()
    {
        ManualTimeProvider time = new(DateTimeOffset.Parse("2026-10-30T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        GrantStore store = new(time);
        store.TryCreateFromUser("session", "code", ComputerGrantTier.Full, ComputerActionOrigin.Dispatch, time.UtcNow.AddHours(2), true, out ComputerGrant capped);
        capped.Expiry.ShouldBe(time.UtcNow.AddMinutes(30));
        capped.AutoApproveTyping.ShouldBeFalse();

        store.TryCreateFromUser("session", "mail", ComputerGrantTier.ViewOnly, ComputerActionOrigin.Dispatch, time.UtcNow.AddMinutes(10), true, out ComputerGrant sooner);
        sooner.Expiry.ShouldBe(time.UtcNow.AddMinutes(10));
        sooner.AutoApproveTyping.ShouldBeFalse();

        time.Advance(TimeSpan.FromMinutes(30));
        store.FindActive("session", "code", ComputerActionOrigin.Dispatch).ShouldBeNull();
    }

    [Fact]
    public void DeviceRevokeWithdrawsDispatchGrantsOnly()
    {
        ManualTimeProvider time = new(DateTimeOffset.UnixEpoch);
        GrantStore store = new(time);
        store.TryCreateFromUser("session", "code", ComputerGrantTier.Full, ComputerActionOrigin.Dispatch, null, false, out _);
        store.TryCreateFromUser("session", "code", ComputerGrantTier.Full, ComputerActionOrigin.Local, null, false, out _);
        store.RevokeDevice();
        store.Snapshot().ShouldHaveSingleItem().Origin.ShouldBe(ComputerActionOrigin.Local);
    }

    [Fact]
    public void SnapshotAndRevokeWorkThroughTheRegistryPort()
    {
        GrantStore concrete = new(new ManualTimeProvider(DateTimeOffset.UnixEpoch));
        concrete.TryCreateFromUser("session", "code", ComputerGrantTier.ViewOnly, ComputerActionOrigin.Local, null, false, out ComputerGrant grant);
        GrantStore registry = concrete;
        registry.Snapshot().ShouldHaveSingleItem().GrantId.ShouldBe(grant.GrantId);
        registry.Revoke(grant.GrantId);
        registry.Snapshot().ShouldBeEmpty();
    }
}
