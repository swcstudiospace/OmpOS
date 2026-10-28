using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests;

/// <summary>
/// The Autonogrammer control library (spec 3.2 to 3.13): one of each stock control is created in each theme variant, its pseudo-classes
/// are forced, and the presenter that Fluent's template draws resolves to the role the spec names. A second group guards the
/// Fluent keys themselves: no brush key of a restyled control may still resolve to a Fluent grey or accent.
/// </summary>
public sealed class ControlStyleTests
{
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>The Fluent key prefixes whose brush keys must all resolve to an Autonogrammer role.</summary>
    private static readonly string[] TwoItems = ["One", "Two"];

    private static readonly char[] SelectorSeparators = ['.', ':', '#', '['];

    private static readonly string[] GuardedPrefixes =
    [
        "ButtonBackground", "ButtonForeground", "ButtonBorderBrush",
        "AccentButtonBackground", "AccentButtonForeground", "AccentButtonBorderBrush",
        "ToggleButtonBackground", "ToggleButtonForeground", "ToggleButtonBorderBrush",
        "TextControlBackground", "TextControlForeground", "TextControlBorderBrush", "TextControlPlaceholderForeground",
        "ComboBoxBackground", "ComboBoxForeground", "ComboBoxBorderBrush", "ComboBoxDropDown", "ComboBoxItemBackground", "ComboBoxItemForeground",
        "TabItemHeaderBackground", "TabItemHeaderForeground", "TabItemHeaderSelectedPipeFill",
        "ScrollBarBackground", "ScrollBarTrackFill", "ScrollBarThumbFill", "ScrollBarForeground",
        "ToolTipBackground", "ToolTipBorderBrush", "ToolTipForeground",
        "FlyoutPresenterBackground", "FlyoutBorderThemeBrush", "MenuFlyoutPresenterBackground", "MenuFlyoutPresenterBorderBrush",
        "MenuFlyoutItemBackground", "MenuFlyoutItemForeground",
        "ToggleSwitchFill", "ToggleSwitchStroke", "ToggleSwitchKnobFill",
        "CheckBoxBackground", "CheckBoxBorderBrush", "CheckBoxCheckBackground", "CheckBoxForeground",
        "RadioButtonBackground", "RadioButtonBorderBrush", "RadioButtonForeground", "RadioButtonOuterEllipse",
        "SystemControlHighlightList",
    ];

    /// <summary>Both theme variants.</summary>
    public static TheoryData<string> Variants => new() { "Light", "Dark" };

    /// <summary>Secondary button: rest, hover, pressed and disabled draw the roles of the 3.3 table.</summary>
    [Theory]
    [MemberData(nameof(Variants))]
    public async Task TheSecondaryButtonDrawsItsRolesInEveryState(string variant)
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var button = new Button { Content = "Retry" };
            var window = Show(variant, button);
            var presenter = PresenterOf(button);

            Is(presenter.Background, variant, "SurfaceRaised");
            Is(presenter.Foreground, variant, "TextPrimary");
            Is(presenter.BorderBrush, variant, "Border");

            Force(button, ":pointerover", true);
            Is(presenter.Background, variant, "Hover");
            Is(presenter.BorderBrush, variant, "BorderStrong");

            Force(button, ":pressed", true);
            Is(presenter.Background, variant, "SurfaceActive");
            Is(presenter.BorderBrush, variant, "BorderStrong");

            Force(button, ":pointerover", false);
            Force(button, ":pressed", false);
            button.IsEnabled = false;
            window.UpdateLayout();
            Is(presenter.Foreground, variant, "TextSecondary");
            button.Opacity.ShouldBe(0.5);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Primary button: the accent fill and its label never change; hover and press dim the whole button.</summary>
    [Theory]
    [MemberData(nameof(Variants))]
    public async Task ThePrimaryButtonKeepsItsFillAndDimsByOpacity(string variant)
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var button = new Button { Content = "Send" };
            button.Classes.Add("primary");
            Show(variant, button);
            var presenter = PresenterOf(button);

            Is(presenter.Background, variant, "AccentPurple");
            Is(presenter.Foreground, variant, "AccentPurpleOn");
            button.Opacity.ShouldBe(1);

            Force(button, ":pointerover", true);
            Is(presenter.Background, variant, "AccentPurple");
            Is(presenter.Foreground, variant, "AccentPurpleOn");
            button.Opacity.ShouldBe(0.92);

            Force(button, ":pressed", true);
            Is(presenter.Background, variant, "AccentPurple");
            button.Opacity.ShouldBe(0.80);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Ghost and danger buttons: no fill at rest; hover tints the fill and the danger label stays StatusError.</summary>
    [Theory]
    [MemberData(nameof(Variants))]
    public async Task GhostAndDangerButtonsTintOnHover(string variant)
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var ghost = new Button { Content = "Close" };
            ghost.Classes.Add("ghost");
            var danger = new Button { Content = "Close agent" };
            danger.Classes.Add("destructive");
            Show(variant, ghost, danger);

            Transparent(PresenterOf(ghost).Background);
            Is(PresenterOf(ghost).Foreground, variant, "TextSecondary");
            Force(ghost, ":pointerover", true);
            Is(PresenterOf(ghost).Background, variant, "Hover");
            Is(PresenterOf(ghost).Foreground, variant, "TextPrimary");

            Is(PresenterOf(danger).Foreground, variant, "StatusError");
            Force(danger, ":pointerover", true);
            Is(PresenterOf(danger).Background, variant, "DiffRemoved");
            Is(PresenterOf(danger).Foreground, variant, "StatusError");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>ToggleButton: checked draws Selected with a strong border and a heavier weight.</summary>
    [Theory]
    [MemberData(nameof(Variants))]
    public async Task ACheckedToggleDrawsTheSelectedRole(string variant)
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var toggle = new ToggleButton { Content = "Tools" };
            Show(variant, toggle);
            var presenter = PresenterOf(toggle);

            Is(presenter.Background, variant, "SurfaceRaised");
            Is(presenter.BorderBrush, variant, "Border");

            toggle.IsChecked = true;
            Is(presenter.Background, variant, "Selected");
            Is(presenter.BorderBrush, variant, "BorderStrong");
            toggle.FontWeight.ShouldBe(FontWeight.Medium);

            Force(toggle, ":pointerover", true);
            Is(presenter.Background, variant, "Selected");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>TextBox: the outline is BorderStrong at rest, TextSecondary on hover, AccentPurple when focused.</summary>
    [Theory]
    [MemberData(nameof(Variants))]
    public async Task ATextBoxDrawsItsOutlineRoles(string variant)
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var box = new TextBox { Text = "abc" };
            Show(variant, box);
            var frame = Part<Border>(box, "PART_BorderElement");

            Is(frame.Background, variant, "SurfaceRaised");
            Is(frame.BorderBrush, variant, "BorderStrong");

            Force(box, ":pointerover", true);
            Is(frame.BorderBrush, variant, "TextSecondary");

            Force(box, ":pointerover", false);
            Force(box, ":focus", true);
            Is(frame.BorderBrush, variant, "AccentPurple");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>ListBoxItem: hover draws Hover, selected draws Selected with the AccentPurple left bar.</summary>
    [Theory]
    [MemberData(nameof(Variants))]
    public async Task AListRowDrawsHoverAndSelectedRoles(string variant)
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var list = new ListBox { ItemsSource = TwoItems };
            Show(variant, list);
            var rows = list.GetVisualDescendants().OfType<ListBoxItem>().ToList();
            rows.Count.ShouldBe(2);

            Force(rows[0], ":pointerover", true);
            Is(PresenterOf(rows[0]).Background, variant, "Hover");

            list.SelectedIndex = 1;
            Is(PresenterOf(rows[1]).Background, variant, "Selected");
            Is(PresenterOf(rows[1]).BorderBrush, variant, "AccentPurple");
            rows[1].BorderThickness.ShouldBe(new Thickness(2, 0, 0, 0));
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>TabStripItem: rest is quiet, hover is Hover, selected shows the AccentPurple pipe.</summary>
    [Theory]
    [MemberData(nameof(Variants))]
    public async Task ATabDrawsHoverAndTheSelectedPipe(string variant)
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var strip = new TabStrip { ItemsSource = TwoItems, SelectedIndex = 0 };
            strip.Classes.Add("top-tabs");
            Show(variant, strip);
            var tabs = strip.GetVisualDescendants().OfType<TabStripItem>().ToList();
            tabs.Count.ShouldBe(2);

            var pipe = Part<Border>(tabs[0], "PART_SelectedPipe");
            pipe.IsVisible.ShouldBeTrue();
            Is(pipe.Background, variant, "AccentPurple");
            Is(tabs[0].Foreground, variant, "TextPrimary");
            Is(tabs[1].Foreground, variant, "TextSecondary");

            Force(tabs[1], ":pointerover", true);
            var root = Part<Border>(tabs[1], "PART_LayoutRoot");
            Is(root.Background, variant, "Hover");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>ComboBox, CheckBox, ToggleSwitch and RadioButton resolve their state brushes to the Autonogrammer roles.</summary>
    [Theory]
    [MemberData(nameof(Variants))]
    public async Task TheRemainingControlsResolveTheirKeyBrushes(string variant)
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var combo = new ComboBox { ItemsSource = TwoItems, SelectedIndex = 0 };
            var check = new CheckBox { Content = "Match case", IsChecked = true };
            var toggleSwitch = new ToggleSwitch { IsChecked = true };
            var radio = new RadioButton { Content = "Dark", IsChecked = true };
            Show(variant, combo, check, toggleSwitch, radio);

            var comboFrame = Part<Border>(combo, "Background");
            Is(comboFrame.Background, variant, "SurfaceRaised");
            Is(comboFrame.BorderBrush, variant, "BorderStrong");

            var box = Part<Border>(check, "NormalRectangle");
            Is(box.Background, variant, "AccentPurple");

            var track = Part<Border>(toggleSwitch, "SwitchKnobBounds");
            Is(track.Background, variant, "AccentPurple");

            var ring = Part<Avalonia.Controls.Shapes.Ellipse>(radio, "CheckOuterEllipse");
            Is(ring.Fill, variant, "AccentPurple");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Tooltip, flyout and menu containers use SurfaceRaised with a Border outline (3.11).</summary>
    [Theory]
    [MemberData(nameof(Variants))]
    public async Task PopupContainersDrawSurfaceRaisedWithAHairline(string variant)
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            Show(variant, new TextBlock { Text = "x" });

            Is(ResolveBrush("ToolTipBackground", variant), variant, "SurfaceRaised");
            Is(ResolveBrush("ToolTipBorderBrush", variant), variant, "Border");
            Is(ResolveBrush("FlyoutPresenterBackground", variant), variant, "SurfaceRaised");
            Is(ResolveBrush("FlyoutBorderThemeBrush", variant), variant, "Border");
            Is(ResolveBrush("MenuFlyoutPresenterBackground", variant), variant, "SurfaceRaised");
            Is(ResolveBrush("MenuFlyoutItemBackgroundPointerOver", variant), variant, "Hover");
            Is(ResolveBrush("MenuFlyoutItemForegroundDisabled", variant), variant, "TextSecondary");
            Is(ResolveBrush("ScrollBarThumbFillPointerOver", variant), variant, "TextSecondary");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Every brush key that Fluent defines for a restyled control resolves, through the application, to an Autonogrammer role or to
    /// transparent. A key that still resolves to a Fluent grey or accent fails here by name.
    /// </summary>
    [Theory]
    [MemberData(nameof(Variants))]
    public async Task NoGuardedFluentBrushKeyStillResolvesToAFluentValue(string variant)
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            Show(variant, new TextBlock { Text = "x" });
            var keys = FluentKeys(variant).Where(static key => GuardedPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal))).Order(StringComparer.Ordinal).ToList();
            keys.Count.ShouldBeGreaterThan(150, "the walk must find the Fluent control keys");

            var offenders = new List<string>();
            foreach (var key in keys)
            {
                var resolved = ResolveResource(key, variant);
                if (resolved is not ISolidColorBrush brush)
                {
                    continue;
                }

                if (!RoleColours(variant).Contains(brush.Color) || brush.Opacity < 1 && brush.Color.A != 0)
                {
                    offenders.Add($"{key} = {brush.Color} (opacity {brush.Opacity})");
                }
            }

            offenders.ShouldBeEmpty("these Fluent keys must be aliased to Autonogrammer roles in AutonogrammerTokens.axaml");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The Fluent metrics the spec names carry its values: radii, tab height, scroll bar size, tooltip size.</summary>
    [Theory]
    [MemberData(nameof(Variants))]
    public async Task TheFluentMetricsCarryTheSpecValues(string variant)
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            Show(variant, new TextBlock { Text = "x" });

            ResolveResource("ControlCornerRadius", variant).ShouldBe(new CornerRadius(6));
            ResolveResource("OverlayCornerRadius", variant).ShouldBe(new CornerRadius(10));
            ResolveResource("ButtonPadding", variant).ShouldBe(new Thickness(11, 0));
            ResolveResource("TabStripItemMinHeight", variant).ShouldBe(28d);
            ResolveResource("ScrollBarSize", variant).ShouldBe(10d);
            ResolveResource("ToolTipContentThemeFontSize", variant).ShouldBe(12d);
            ResolveResource("ToolTipContentMaxWidth", variant).ShouldBe(360d);
            ResolveResource("ControlContentThemeFontSize", variant).ShouldBe(13d);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The control library declares a transition only where it also clears it under the reduced-motion class.</summary>
    [Fact]
    public void EveryTransitionTheLibraryDeclaresHasAReducedMotionOverride()
    {
        var path = Path.Combine(ShellHarness.RepoRoot(), "src", "Autonogrammer.Native.Views", "Themes", "AutonogrammerControls.axaml");
        var styles = XDocument.Load(path).Descendants().Where(static element => string.Equals(element.Name.LocalName, "Style", StringComparison.Ordinal)).ToList();

        var declared = styles
            .Where(static style => !Selector(style).Contains("reduced-motion", StringComparison.Ordinal) && HasTransitionSetter(style, expectTransition: true))
            .Select(static style => LastType(Selector(style)))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var cleared = styles
            .Where(static style => Selector(style).Contains("reduced-motion", StringComparison.Ordinal) && HasTransitionSetter(style, expectTransition: false))
            .Select(static style => LastType(Selector(style)))
            .ToHashSet(StringComparer.Ordinal);

        declared.ShouldNotBeEmpty();
        declared.Where(type => !cleared.Contains(type)).ShouldBeEmpty("a control type with a transition needs a .reduced-motion override");
    }

    private static Window Show(string variant, params Control[] controls)
    {
        var panel = new StackPanel { Spacing = 8 };
        foreach (var control in controls)
        {
            panel.Children.Add(control);
        }

        var window = new Window
        {
            Content = panel,
            Width = 400,
            Height = 600,
            RequestedThemeVariant = string.Equals(variant, "Dark", StringComparison.Ordinal) ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        window.Show();
        ShellHarness.Settle(window);
        return window;
    }

    private static ContentPresenter PresenterOf(TemplatedControl control)
    {
        control.ApplyTemplate();
        return Part<ContentPresenter>(control, "PART_ContentPresenter");
    }

    private static T Part<T>(Visual root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(part => string.Equals(part.Name, name, StringComparison.Ordinal));

    private static void Force(Control control, string pseudoClass, bool on)
    {
        ((IPseudoClasses)control.Classes).Set(pseudoClass, on);
        (TopLevel.GetTopLevel(control) as Window)?.UpdateLayout();
    }

    private static Color RoleColour(string variant, string role)
    {
        var brush = ResolveBrush("Autonogrammer.Color." + role, variant);
        return ((ISolidColorBrush)brush!).Color;
    }

    private static void Is(IBrush? actual, string variant, string role)
    {
        actual.ShouldBeAssignableTo<ISolidColorBrush>($"{role} must be a solid brush");
        ((ISolidColorBrush)actual!).Color.ShouldBe(RoleColour(variant, role), $"{variant}: expected the {role} role");
    }

    private static void Transparent(IBrush? actual)
    {
        if (actual is ISolidColorBrush brush)
        {
            brush.Color.A.ShouldBe((byte)0);
        }
        else
        {
            actual.ShouldBeNull();
        }
    }

    private static IBrush? ResolveBrush(string key, string variant) => ResolveResource(key, variant) as IBrush;

    private static object? ResolveResource(string key, string variant)
    {
        var application = Application.Current!;
        application.TryGetResource(key, string.Equals(variant, "Dark", StringComparison.Ordinal) ? ThemeVariant.Dark : ThemeVariant.Light, out var value).ShouldBeTrue($"{key} must resolve in {variant}");
        return value;
    }

    private static HashSet<Color> RoleColours(string variant)
    {
        var colours = new HashSet<Color>();
        foreach (var role in OmpRoleNames())
        {
            colours.Add(RoleColour(variant, role));
        }

        colours.Add(Colors.Transparent);
        return colours;
    }

    private static IEnumerable<string> OmpRoleNames()
    {
        var key = XName.Get("Key", XamlNamespace);
        var path = Path.Combine(ShellHarness.RepoRoot(), "src", "Autonogrammer.Native.Views", "Themes", "AutonogrammerTokens.axaml");
        return XDocument.Load(path).Descendants()
            .Select(static element => (string?)element.Attribute(XName.Get("Key", XamlNamespace)))
            .Where(static name => name is not null && name.StartsWith("Autonogrammer.Color.", StringComparison.Ordinal))
            .Select(static name => name!["Autonogrammer.Color.".Length..])
            .Distinct(StringComparer.Ordinal);
    }

    /// <summary>Every key Fluent's theme dictionary defines for the variant, found by walking its merged and theme dictionaries.</summary>
    private static HashSet<string> FluentKeys(string variant)
    {
        var theme = Application.Current!.Styles.OfType<FluentTheme>().Single();
        var found = new HashSet<string>(StringComparer.Ordinal);
        Walk(theme.Resources, string.Equals(variant, "Dark", StringComparison.Ordinal) ? ThemeVariant.Dark : ThemeVariant.Light, found);
        return found;
    }

    private static void Walk(IResourceProvider? provider, ThemeVariant variant, HashSet<string> found)
    {
        if (provider is not ResourceDictionary dictionary)
        {
            return;
        }

        foreach (var key in dictionary.Keys)
        {
            if (key is string name)
            {
                found.Add(name);
            }
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            Walk(merged, variant, found);
        }

        foreach (var (themeKey, themed) in dictionary.ThemeDictionaries)
        {
            if (Equals(themeKey, variant) || Equals(themeKey, ThemeVariant.Default))
            {
                Walk(themed as IResourceProvider, variant, found);
            }
        }
    }

    private static string Selector(XElement style) => (string?)style.Attribute("Selector") ?? string.Empty;

    private static bool HasTransitionSetter(XElement style, bool expectTransition)
    {
        var setter = style.Elements().FirstOrDefault(static element => string.Equals(element.Name.LocalName, "Setter", StringComparison.Ordinal) && string.Equals((string?)element.Attribute("Property"), "Transitions", StringComparison.Ordinal));
        if (setter is null)
        {
            return false;
        }

        var has = setter.Descendants().Any(static element => element.Name.LocalName.EndsWith("Transition", StringComparison.Ordinal));
        return has == expectTransition;
    }

    private static string LastType(string selector)
    {
        var last = selector.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
        var end = last.IndexOfAny(SelectorSeparators);
        return end < 0 ? last : last[..end];
    }
}
