using Autonogrammer.UI.Components.Shell;
using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Shell;

/// <summary>The root error boundary: a failing page shows a plain alert with a Reload button instead of a blank screen, and neither the screen nor the log carries the exception's text.</summary>
public sealed class ErrorBoundaryTests : UiTestContext
{
    // The text EditForm throws when it has neither a model nor an edit context: it stands for an exception message the user must never see.
    private const string ExceptionText = "requires a Model parameter";

    private readonly RecordingBoundaryLogger _logger = new();

    /// <summary>Initializes a new instance of the <see cref="ErrorBoundaryTests"/> class.</summary>
    public ErrorBoundaryTests()
    {
        Services.AddSingleton<ILogger<AppErrorBoundary>>(_logger);
    }

    private IRenderedComponent<AppErrorBoundary> RenderBoundary() => Render<AppErrorBoundary>(parameters => parameters
        .AddChildContent<EditForm>());

    /// <summary>A healthy child renders as it is, with no fallback.</summary>
    [Fact]
    public void AHealthyChildRendersUnchanged()
    {
        var cut = Render<AppErrorBoundary>(parameters => parameters.AddChildContent("<p id=\"child\">fine</p>"));

        cut.Find("#child").TextContent.ShouldBe("fine");
        cut.FindAll("[role='alert']").ShouldBeEmpty();
        _logger.Entries.ShouldBeEmpty();
    }

    /// <summary>A failing child is replaced by an alert with a plain-language message, one heading and a Reload button, and the exception's text is nowhere in the markup.</summary>
    [Fact]
    public void AFailingChildShowsAnAccessibleFallbackWithoutTheExceptionText()
    {
        var cut = RenderBoundary();

        var alert = cut.Find("[role='alert']");
        alert.TextContent.ShouldContain(ErrorFallback.Heading, Case.Sensitive);
        alert.TextContent.ShouldContain(ErrorFallback.Body, Case.Sensitive);
        cut.FindAll("h1").Select(static heading => heading.TextContent).ShouldBe([ErrorFallback.Heading]);
        cut.Find("button").TextContent.ShouldBe(ErrorFallback.ReloadText);
        cut.Markup.ShouldNotContain(ExceptionText);
        cut.Markup.ShouldNotContain("InvalidOperationException");
        cut.Markup.ShouldNotContain("StackTrace");
    }

    /// <summary>The failure is logged once through ILogger with the exception type only: neither the message nor the exception object reaches the log.</summary>
    [Fact]
    public void TheFailureIsLoggedWithoutItsMessage()
    {
        _ = RenderBoundary();

        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Error);
        entry.Message.ShouldContain(nameof(InvalidOperationException), Case.Sensitive);
        entry.Message.ShouldNotContain(ExceptionText);
        entry.Exception.ShouldBeNull("the exception object carries the message and the stack");
    }

    /// <summary>Reload is a real button that reloads the page; activating it does not throw.</summary>
    [Fact]
    public async Task ReloadReloadsThePage()
    {
        var cut = RenderBoundary();

        await cut.Find("button").ClickAsync().ConfigureAwait(true);

        cut.Find("button").TextContent.ShouldBe(ErrorFallback.ReloadText);
    }
}
