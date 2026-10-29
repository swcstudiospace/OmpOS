using Autonogrammer.UI.Components.Auth;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Auth;

/// <summary>The connect page: a real form with a labelled password field, a check against the API, and errors that are announced while the focus stays in the form.</summary>
public sealed class ConnectViewTests : UiTestContext
{
    private const string TokenSelector = "#autonogrammer-connect-token";
    private const string Token = "T0ken-value_for-the-test";

    private readonly FakeAuthSession _session = new();
    private readonly FakeConnector _connector = new();

    /// <summary>Initializes a new instance of the <see cref="ConnectViewTests"/> class.</summary>
    public ConnectViewTests()
    {
        Services.AddSingleton<IAuthSession>(_session);
        Services.AddSingleton<IOwnerTokenConnector>(_connector);
    }

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    private static async Task TypeAndSubmitAsync(IRenderedComponent<ConnectView> cut, string token)
    {
        await cut.Find(TokenSelector).InputAsync(new ChangeEventArgs { Value = token }).ConfigureAwait(true);
        await cut.Find("form").SubmitAsync().ConfigureAwait(true);
    }

    /// <summary>The page has one h1, a real form, a visible label tied to the field, a password type, the current-password hint and help text the field is described by.</summary>
    [Fact]
    public void TheFormIsLabelledAndTypedForAPasswordManager()
    {
        var cut = Render<ConnectView>();

        cut.FindAll("h1").Select(static heading => heading.TextContent).ShouldBe([AuthCopy.ConnectHeading]);
        cut.FindAll("main").Count.ShouldBe(1);
        var form = cut.Find("form");
        var label = form.QuerySelector("label[for='autonogrammer-connect-token']");
        label.ShouldNotBeNull();
        label.TextContent.ShouldBe(AuthCopy.TokenLabel);
        label.ClassList.ShouldNotContain("autonogrammer-sr-only", StringComparer.Ordinal, "the label is visible");
        var field = cut.Find(TokenSelector);
        field.GetAttribute("type").ShouldBe("password");
        field.GetAttribute("autocomplete").ShouldBe("current-password");
        field.GetAttribute("aria-required").ShouldBe("true");
        field.GetAttribute("aria-invalid").ShouldBeNull();
        var describedBy = field.GetAttribute("aria-describedby");
        describedBy.ShouldNotBeNull();
        cut.Find("#" + describedBy).TextContent.ShouldBe(AuthCopy.ConnectHelp);
        var submit = form.QuerySelector("button[type='submit']");
        submit.ShouldNotBeNull();
        submit.TextContent.ShouldBe(AuthCopy.Connect);
    }

    /// <summary>A visually hidden, read-only username field with a label lets a password manager store the token as a password for this site.</summary>
    [Fact]
    public void AHiddenReadOnlyUsernameFieldAccompaniesThePasswordField()
    {
        var cut = Render<ConnectView>();

        var username = cut.Find("input[autocomplete='username']");
        username.HasAttribute("readonly").ShouldBeTrue();
        username.ClassList.ShouldContain("autonogrammer-sr-only", StringComparer.Ordinal);
        username.GetAttribute("value").ShouldBe(AuthCopy.Username);
        cut.Find("label[for='" + username.Id + "']").TextContent.ShouldBe(AuthCopy.UsernameLabel);
    }

    /// <summary>The API accepting the token sends the tab to the page it asked for, clears the field and forgets the remembered path.</summary>
    [Fact]
    public async Task AnAcceptedTokenReturnsToTheRememberedPage()
    {
        _session.ReturnPath = "sessions/abc";
        _connector.Enqueue(ConnectOutcome.Connected);
        var cut = Render<ConnectView>();

        await TypeAndSubmitAsync(cut, "  " + Token + "\n").ConfigureAwait(true);

        _connector.Tokens.ShouldBe([Token], "whitespace that came with a paste is not part of the token");
        Navigation.ToBaseRelativePath(Navigation.Uri).ShouldBe("sessions/abc");
        _session.ReturnPath.ShouldBeNull();
        cut.Markup.ShouldNotContain(Token);
    }

    /// <summary>Without a remembered page a successful connect goes to the home page; a remembered page on another site is refused.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    [InlineData("connect")]
    public async Task AnAcceptedTokenGoesHomeWhenThereIsNoSafePathToReturnTo(string? remembered)
    {
        Navigation.NavigateTo(AuthRoutes.ConnectPath);
        _session.ReturnPath = remembered;
        _connector.Enqueue(ConnectOutcome.Connected);
        var cut = Render<ConnectView>();

        await TypeAndSubmitAsync(cut, Token).ConfigureAwait(true);

        Navigation.ToBaseRelativePath(Navigation.Uri).ShouldBeEmpty();
    }

    /// <summary>A refused token shows an inline alert, empties the field so the wrong token is not shown again, marks the field invalid and keeps the focus in the form.</summary>
    [Fact]
    public async Task ARefusedTokenShowsAnAlertClearsTheFieldAndKeepsTheFocus()
    {
        _connector.Enqueue(ConnectOutcome.Rejected);
        var cut = Render<ConnectView>();
        var fieldId = cut.Find(TokenSelector).InitialReferenceId();

        await TypeAndSubmitAsync(cut, Token).ConfigureAwait(true);

        var alert = cut.Find("[role='alert']");
        alert.TextContent.ShouldBe(AuthCopy.TokenRejected);
        var field = cut.Find(TokenSelector);
        (field.GetAttribute("value") ?? string.Empty).ShouldBeEmpty();
        field.GetAttribute("aria-invalid").ShouldBe("true");
        field.GetAttribute("aria-describedby").ShouldNotBeNull().ShouldContain(alert.Id.ShouldNotBeNull());
        cut.Markup.ShouldNotContain(Token);
        JSInterop.FocusCalls()[^1].ReferenceIdOf().ShouldBe(fieldId);
        Navigation.ToBaseRelativePath(Navigation.Uri).ShouldBeEmpty();
    }

    /// <summary>A network failure says so in different words than a refused token, keeps what was typed, and the same button tries again.</summary>
    [Fact]
    public async Task ANetworkFailureKeepsTheTokenAndCanBeRetried()
    {
        _connector.Enqueue(ConnectOutcome.Unreachable);
        _connector.Enqueue(ConnectOutcome.Connected);
        var cut = Render<ConnectView>();

        await TypeAndSubmitAsync(cut, Token).ConfigureAwait(true);

        cut.Find("[role='alert']").TextContent.ShouldBe(AuthCopy.ConnectUnreachable);
        string.Equals(cut.Find("[role='alert']").TextContent, AuthCopy.TokenRejected, StringComparison.Ordinal).ShouldBeFalse("a network failure must not say the token was rejected");
        cut.Find(TokenSelector).GetAttribute("value").ShouldBe(Token);

        await cut.Find("form").SubmitAsync().ConfigureAwait(true);

        _connector.Tokens.ShouldBe([Token, Token]);
        cut.FindAll("[role='alert']").ShouldBeEmpty();
        Navigation.ToBaseRelativePath(Navigation.Uri).ShouldBeEmpty();
    }

    /// <summary>An empty field is reported without calling the API, and the focus returns to the field.</summary>
    [Fact]
    public async Task AnEmptyFieldIsReportedWithoutCallingTheApi()
    {
        var cut = Render<ConnectView>();
        var fieldId = cut.Find(TokenSelector).InitialReferenceId();

        await cut.Find("form").SubmitAsync().ConfigureAwait(true);

        cut.Find("[role='alert']").TextContent.ShouldBe(AuthCopy.TokenRequired);
        _connector.Tokens.ShouldBeEmpty();
        JSInterop.FocusCalls()[^1].ReferenceIdOf().ShouldBe(fieldId);
    }

    /// <summary>While the token is checked a status says so, the field is read-only, and a second submit is ignored.</summary>
    [Fact]
    public async Task WhileTheTokenIsCheckedASecondSubmitIsIgnored()
    {
        var answer = _connector.EnqueuePending();
        var cut = Render<ConnectView>();
        await cut.Find(TokenSelector).InputAsync(new ChangeEventArgs { Value = Token }).ConfigureAwait(true);

        var first = cut.Find("form").SubmitAsync();

        await cut.WaitForAssertionAsync(() => cut.Find("[role='status']").TextContent.ShouldBe(AuthCopy.Checking)).ConfigureAwait(true);
        cut.Find(TokenSelector).HasAttribute("readonly").ShouldBeTrue();
        await cut.Find("form").SubmitAsync().ConfigureAwait(true);
        _connector.Tokens.Count.ShouldBe(1);

        answer.SetResult(ConnectOutcome.Rejected);
        await first.ConfigureAwait(true);

        cut.Find("[role='alert']").TextContent.ShouldBe(AuthCopy.TokenRejected);
        cut.FindAll("[role='status']").ShouldBeEmpty();
    }

    /// <summary>A session that ended because the API refused its token says so, in an alert, above the form.</summary>
    [Fact]
    public void AnEndedSessionIsAnnounced()
    {
        _session.SessionEnded = true;

        var cut = Render<ConnectView>();

        cut.Find("[role='alert']").TextContent.ShouldBe(AuthCopy.SessionEnded);
        cut.Markup.IndexOf(AuthCopy.SessionEnded, StringComparison.Ordinal).ShouldBeLessThan(cut.Markup.IndexOf("<form", StringComparison.Ordinal), "the notice comes before the form");
    }
}
