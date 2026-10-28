using System.Buffers.Binary;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using OmpOS.Native.Tests.Settings;
using OmpOS.Native.Views;
using OmpOS.Native.Views.Branding;
using OmpOS.Presentation.About;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.About;

/// <summary>The About view and window: what they show, the keyboard order, Escape, the links through the launcher seam and the copy button.</summary>
public sealed class AboutViewTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    /// <summary>Every focusable control and the live region has an accessible name, the window is named, and the texts carry the version, revision, licence, source address and source offer.</summary>
    [Fact]
    public async Task EverythingIsNamedAndTheSourceOfferIsShown()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = AboutModel.Create("0.15.0+" + Sha, string.Empty);
            var window = new AboutWindow(model, new FakeUrlLauncher());
            window.Show();
            SettingsTree.SettleSafe(window);

            window.Title.ShouldBe(AboutCopy.WindowTitle);
            AutomationProperties.GetName(window).ShouldBe(AboutCopy.WindowTitle);
            SettingsTree.Unnamed(window.View).ShouldBeEmpty();
            var texts = SettingsTree.Texts(window.View);
            texts.ShouldContain("OmpOS", StringComparer.Ordinal);
            texts.ShouldContain("0.15.0", StringComparer.Ordinal);
            texts.ShouldContain(Sha, StringComparer.Ordinal);
            texts.ShouldContain("AGPL-3.0-or-later", StringComparer.Ordinal);
            texts.ShouldContain("https://github.com/swcstudiospace/OmpOS", StringComparer.Ordinal);
            texts.ShouldContain(AboutCopy.SourceOffer, StringComparer.Ordinal);
            AutomationProperties.GetName(window.View.Mark).ShouldBe(AboutCopy.MarkName);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The brand mark is drawn from the resource at its full 256 pixel size, and the window carries the application icon.</summary>
    [Fact]
    public async Task TheMarkAndTheWindowIconAreTheBrandResources()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = new AboutWindow(AboutModel.Create("0.15.0", string.Empty), new FakeUrlLauncher());
            window.Show();
            SettingsTree.SettleSafe(window);

            // The headless platform decodes every bitmap to a 1 by 1 stub, so the pixel size is read from the PNG header of the resource the image draws.
            window.View.Mark.Source.ShouldBeOfType<Bitmap>();
            using var stream = AssetLoader.Open(new Uri(AppIcon.ResourceUri));
            var header = new byte[24];
            stream.ReadExactly(header);
            BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4)).ShouldBe(256);
            BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20, 4)).ShouldBe(256);
            window.Icon.ShouldNotBeNull();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Tab visits the five links in order, then Copy version info, then Close, and every stop is at least the target minimum.</summary>
    [Fact]
    public async Task TheFocusOrderIsTheLinksThenCopyThenClose()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = AboutModel.Create("0.15.0+" + Sha, string.Empty);
            var (window, _, _) = Host(model);

            var stops = SettingsTree.TabStops(window).Select(static control => AutomationProperties.GetName(control)).ToArray();

            stops.ShouldBe([.. model.Links.Select(static link => link.AccessibleName), AboutCopy.CopyLabel, AboutCopy.CloseLabel]);
            SettingsTree.TabStops(window).ShouldAllBe(static control => control.Bounds.Height >= 24 && control.Bounds.Width >= 24);

            SettingsTree.PressTab(window);
            SettingsTree.FocusedName(window).ShouldBe(model.Links[0].AccessibleName);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Escape closes the About window, and so does the Close button.</summary>
    [Fact]
    public async Task EscapeAndCloseDismissTheWindow()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var byEscape = new AboutWindow(AboutModel.Create("0.15.0", string.Empty), new FakeUrlLauncher());
            byEscape.Show();
            SettingsTree.SettleSafe(byEscape);
            byEscape.IsVisible.ShouldBeTrue();

            SettingsTree.Press(byEscape, Key.Escape, PhysicalKey.Escape);

            byEscape.IsVisible.ShouldBeFalse();

            var byButton = new AboutWindow(AboutModel.Create("0.15.0", string.Empty), new FakeUrlLauncher());
            byButton.Show();
            SettingsTree.SettleSafe(byButton);

            SettingsTree.Click(byButton, byButton.View.Close);

            byButton.IsVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Each link opens its own address through the launcher, once, and nothing else is opened.</summary>
    [Fact]
    public async Task EachLinkOpensItsAddressThroughTheLauncher()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = AboutModel.Create("0.15.0+" + Sha, string.Empty);
            var (window, view, launcher) = Host(model);
            var buttons = SettingsTree.Visible<Button>(view.Links).ToArray();
            buttons.Length.ShouldBe(model.Links.Count);

            foreach (var button in buttons)
            {
                SettingsTree.Click(window, button);
            }

            launcher.Opened.Select(static uri => uri.AbsoluteUri).ShouldBe(model.Links.Select(static link => new Uri(link.Location).AbsoluteUri));
            model.Status.ShouldBeEmpty();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A launcher that cannot start a browser makes the view announce which address failed.</summary>
    [Fact]
    public async Task AFailedLaunchIsAnnounced()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = AboutModel.Create("0.15.0", string.Empty);
            var (window, view, launcher) = Host(model);
            launcher.Succeeds = false;

            SettingsTree.Click(window, SettingsTree.Visible<Button>(view.Links)[0]);

            view.Status.Text.ShouldNotBeNull().ShouldStartWith(model.Links[0].Label, Case.Sensitive);
            view.Status.Text.ShouldContain("could not be opened", Case.Sensitive);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Copy version info puts the model's text on the clipboard and the live region says so.</summary>
    [Fact]
    public async Task CopyVersionInfoWritesTheClipboardAndAnnouncesIt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = AboutModel.Create("0.15.0+" + Sha, "Test platform");
            var clipboard = new FakeAboutClipboard();
            var (window, view, _) = Host(model, clipboard);

            SettingsTree.Click(window, view.Copy);

            clipboard.Texts.ShouldBe([model.VersionInfoText]);
            view.Status.Text.ShouldBe(AboutCopy.Copied);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static (Window Window, AboutView View, FakeUrlLauncher Launcher) Host(AboutModel model, FakeAboutClipboard? clipboard = null)
    {
        var launcher = new FakeUrlLauncher();
        var view = new AboutView(model, launcher, clipboard ?? new FakeAboutClipboard());
        var window = new Window { Content = view, Width = 560, Height = 620 };
        window.Show();
        SettingsTree.SettleSafe(window);
        return (window, view, launcher);
    }
}
