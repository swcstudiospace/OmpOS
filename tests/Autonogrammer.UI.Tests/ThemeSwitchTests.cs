using Bunit;
using Autonogrammer.UI.Components.Shell;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>The System, Light and Dark switch reads the saved theme and persists the choice through the theme JS module (PX-08).</summary>
public sealed class ThemeSwitchTests : UiTestContext
{
    private const string ThemeModule = "./_content/Autonogrammer.UI/scripts/autonogrammer-theme.js";

    /// <summary>The switch is a named group of three toggle buttons, in the order System, Light, Dark.</summary>
    [Fact]
    public void SwitchIsANamedGroupOfThreeToggleButtons()
    {
        JSInterop.SetupModule(ThemeModule);

        var cut = Render<ThemeSwitch>();

        var group = cut.Find("[role='group']");
        group.GetAttribute("aria-label").ShouldBe("Theme");
        cut.FindAll("button").Select(static button => button.TextContent).ShouldBe(["System", "Light", "Dark"]);
        cut.FindAll("button").ShouldAllBe(button => button.GetAttribute("type") == "button" && button.HasAttribute("aria-pressed"));
    }

    /// <summary>The saved theme is the pressed button once the module has answered; nothing is saved means Light.</summary>
    [Theory]
    [InlineData("system", "System")]
    [InlineData("light", "Light")]
    [InlineData("dark", "Dark")]
    public void SavedThemeIsShownAsPressed(string saved, string pressedLabel)
    {
        var module = JSInterop.SetupModule(ThemeModule);
        module.Setup<string>("getTheme").SetResult(saved);

        var cut = Render<ThemeSwitch>();

        AssertPressed(cut, pressedLabel);
    }

    /// <summary>With nothing saved, and with a value the module should never return, the default Light theme is shown.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("purple")]
    public void MissingOrUnknownSavedThemeShowsLight(string? saved)
    {
        var module = JSInterop.SetupModule(ThemeModule);
        module.Setup<string>("getTheme").SetResult(saved!);

        var cut = Render<ThemeSwitch>();

        AssertPressed(cut, "Light");
    }

    /// <summary>Choosing a theme persists it through the module and moves the pressed state; the module is asked once per choice.</summary>
    [Theory]
    [InlineData("System", "system")]
    [InlineData("Light", "light")]
    [InlineData("Dark", "dark")]
    public void ChoosingAThemePersistsItThroughTheModule(string label, string expectedKey)
    {
        var module = JSInterop.SetupModule(ThemeModule);
        module.Setup<string>("getTheme").SetResult("light");
        module.SetupVoid("setTheme", expectedKey);
        var cut = Render<ThemeSwitch>();

        cut.FindAll("button").Single(button => string.Equals(button.TextContent, label, StringComparison.Ordinal)).Click();

        module.VerifyInvoke("setTheme").Arguments.ShouldBe([expectedKey]);
        AssertPressed(cut, label);
    }

    private static void AssertPressed(IRenderedComponent<ThemeSwitch> cut, string label)
    {
        cut.WaitForAssertion(() =>
        {
            var pressed = cut.FindAll("button[aria-pressed='true']").Select(static button => button.TextContent).ToList();
            pressed.ShouldBe([label]);
        });
    }
}
