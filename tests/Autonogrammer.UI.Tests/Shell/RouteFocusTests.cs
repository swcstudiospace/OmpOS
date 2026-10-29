using Autonogrammer.UI.Components.Shell;
using Bunit;
using Microsoft.AspNetCore.Components;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests.Shell;

/// <summary>The route focus moves focus to a page heading only after the page type changes, never on the first page, so the skip link stays the first Tab stop.</summary>
public sealed class RouteFocusTests : UiTestContext
{
    private const string FocusBySelector = "Blazor._internal.domWrapper.focusBySelector";

    private static readonly Type FirstPage = typeof(NotFoundView);
    private static readonly Type SecondPage = typeof(ViewTabs);

    private static RouteData Route(Type page) => new(page, new Dictionary<string, object?>(StringComparer.Ordinal));

    private int FocusCalls() =>
        JSInterop.Invocations.Count(static call => string.Equals(call.Identifier, FocusBySelector, StringComparison.Ordinal));

    /// <summary>The first page that renders does not take focus: the browser's initial focus order, which starts with the skip link, stays.</summary>
    [Fact]
    public void TheFirstPageDoesNotTakeFocus()
    {
        _ = Render<RouteFocus>(parameters => parameters.Add(static p => p.RouteData, Route(FirstPage)));

        FocusCalls().ShouldBe(0);
    }

    /// <summary>Navigating to a page of another type focuses the configured selector once.</summary>
    [Fact]
    public void AChangeOfPageTypeFocusesTheSelectorOnce()
    {
        var cut = Render<RouteFocus>(parameters => parameters
            .Add(static p => p.RouteData, Route(FirstPage))
            .Add(static p => p.Selector, "main h1"));

        cut.Render(parameters => parameters
            .Add(static p => p.RouteData, Route(SecondPage))
            .Add(static p => p.Selector, "main h1"));

        FocusCalls().ShouldBe(1);
        JSInterop.Invocations.Single(static call => string.Equals(call.Identifier, FocusBySelector, StringComparison.Ordinal)).Arguments[0].ShouldBe("main h1");
    }

    /// <summary>A new render of the same page type, for example another route value, leaves focus alone.</summary>
    [Fact]
    public void ASamePageTypeLeavesFocusAlone()
    {
        var cut = Render<RouteFocus>(parameters => parameters.Add(static p => p.RouteData, Route(FirstPage)));

        cut.Render(parameters => parameters.Add(static p => p.RouteData, Route(FirstPage)));
        cut.Render(parameters => parameters.Add(static p => p.RouteData, Route(FirstPage)));

        FocusCalls().ShouldBe(0);
    }

    /// <summary>The selector defaults to the first heading of the document.</summary>
    [Fact]
    public void TheSelectorDefaultsToTheFirstHeading()
    {
        var cut = Render<RouteFocus>(parameters => parameters.Add(static p => p.RouteData, Route(FirstPage)));

        cut.Render(parameters => parameters.Add(static p => p.RouteData, Route(SecondPage)));

        JSInterop.Invocations.Single(static call => string.Equals(call.Identifier, FocusBySelector, StringComparison.Ordinal)).Arguments[0].ShouldBe("h1");
    }
}
