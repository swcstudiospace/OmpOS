using System.Reflection;
using Autonogrammer.Web.Services;
using Bunit;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>The owner token of a tab: held in memory only, signed in only once the API accepted it, ended once however many requests are refused, and never near the browser's storage.</summary>
public sealed class OwnerTokenSessionTests : BunitContext
{
    private const string Token = "owner-token-value-123";

    /// <summary>An offered token is sent but does not sign the tab in; confirming it does, once.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task AnOfferedTokenIsSentButOnlyAConfirmedOneSignsIn()
    {
        var session = new OwnerTokenSession();
        var changes = 0;
        session.Changed += (_, _) => changes++;

        session.Offer(Token);

        session.HasToken.ShouldBeFalse();
        (await session.GetTokenAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(Token);
        changes.ShouldBe(0);

        session.Confirm().ShouldBeTrue();

        session.HasToken.ShouldBeTrue();
        changes.ShouldBe(1);
        session.Confirm().ShouldBeTrue();
        changes.ShouldBe(1, "confirming twice changes nothing");
    }

    /// <summary>With no token to confirm, confirming does nothing and says so.</summary>
    [Fact]
    public void ConfirmingWithoutATokenDoesNothing()
    {
        var session = new OwnerTokenSession();

        session.Confirm().ShouldBeFalse();

        session.HasToken.ShouldBeFalse();
    }

    /// <summary>Signing out forgets the token and raises one change; signing out again changes nothing.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ClearForgetsTheTokenOnce()
    {
        var session = new OwnerTokenSession();
        session.SetToken(Token);
        var changes = 0;
        session.Changed += (_, _) => changes++;

        session.Clear();
        session.Clear();

        session.HasToken.ShouldBeFalse();
        session.SessionEnded.ShouldBeFalse("signing out is not an ended session");
        (await session.GetTokenAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeNull();
        changes.ShouldBe(1);
    }

    /// <summary>A refused token that had been accepted ends the session with a notice, once, however many requests are refused.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ExpiringAnAcceptedTokenEndsTheSessionOnce()
    {
        var session = new OwnerTokenSession();
        session.SetToken(Token);
        var changes = 0;
        session.Changed += (_, _) => changes++;

        for (var refused = 0; refused < 10; refused++)
        {
            session.Expire();
        }

        session.HasToken.ShouldBeFalse();
        session.SessionEnded.ShouldBeTrue();
        (await session.GetTokenAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeNull();
        changes.ShouldBe(1);
    }

    /// <summary>A token that was only offered is dropped silently when refused: the connect page reports that itself.</summary>
    [Fact]
    public void ExpiringAnOfferedTokenIsSilent()
    {
        var session = new OwnerTokenSession();
        var changes = 0;
        session.Changed += (_, _) => changes++;
        session.Offer(Token);

        session.Expire();

        session.SessionEnded.ShouldBeFalse();
        changes.ShouldBe(0);
        session.Confirm().ShouldBeFalse("the refused token is gone");
    }

    /// <summary>The next accepted token clears the "session ended" notice and signs the tab in again.</summary>
    [Fact]
    public void ANewAcceptedTokenClearsTheEndedNotice()
    {
        var session = new OwnerTokenSession();
        session.SetToken(Token);
        session.Expire();
        var changes = 0;
        session.Changed += (_, _) => changes++;

        session.SetToken(Token);

        session.SessionEnded.ShouldBeFalse();
        session.HasToken.ShouldBeTrue();
        changes.ShouldBe(1);
    }

    /// <summary>The remembered return path is kept in memory with the session and nowhere else.</summary>
    [Fact]
    public void TheReturnPathIsKeptInMemory()
    {
        var session = new OwnerTokenSession { ReturnPath = "sessions/abc" };

        session.ReturnPath.ShouldBe("sessions/abc");
    }

    /// <summary>The session never calls JavaScript (no storage, no cookie): strict interop records nothing, and no member of the type holds a JavaScript runtime or anything but the token, two flags and the event.</summary>
    [Fact]
    public void TheSessionNeverTouchesJavaScript()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        var session = new OwnerTokenSession();

        session.SetToken(Token);
        session.Expire();
        session.SetToken(Token);
        session.Clear();

        JSInterop.Invocations.ShouldBeEmpty();
        var fieldTypes = typeof(OwnerTokenSession)
            .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(static field => field.FieldType)
            .ToHashSet();
        fieldTypes.ShouldBeSubsetOf([typeof(string), typeof(bool), typeof(EventHandler)]);
    }
}
