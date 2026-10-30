using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Settings.Privacy;
using Avalonia.Controls;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>The Privacy page view in a headless window: its statements, that they all come from the copy class, and that it has no control to operate.</summary>
public sealed class PrivacyPageViewTests
{
    private static Window Show(PrivacySettingsPage page, out PrivacyPageView view)
    {
        view = new PrivacyPageView { DataContext = page };
        var window = new Window { Width = 900, Height = 1200, Content = view };
        window.Show();
        SettingsTree.SettleSafe(window);
        return window;
    }

    /// <summary>The view draws the heading, the description and the five statements, each row named with its heading and sentence, and nothing else.</summary>
    [Fact]
    public async Task DrawsTheFiveStatementsFromTheCopyAndNothingElse()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = Show(new PrivacySettingsPage(), out var view);
            try
            {
                var texts = SettingsTree.Texts(view);

                texts.ShouldContain(PrivacyCopy.Heading, StringComparer.Ordinal);
                texts.ShouldContain(PrivacyCopy.Description, StringComparer.Ordinal);
                foreach (var row in PrivacyCopy.Rows)
                {
                    texts.ShouldContain(row.Heading, StringComparer.Ordinal);
                    texts.ShouldContain(row.Text, StringComparer.Ordinal);
                    SettingsTree.Has<Border>(view, row.Summary).ShouldBeTrue();
                }

                var allowed = new HashSet<string>(
                    [PrivacyCopy.Heading, PrivacyCopy.Description, .. PrivacyCopy.Rows.SelectMany(static row => new[] { row.Heading, row.Text, row.Summary })],
                    StringComparer.Ordinal);
                SettingsTree.Texts(view).ShouldAllBe(text => allowed.Contains(text), "every visible text comes from PrivacyCopy");
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The page has nothing to press or type in: it offers no keyboard stop and every authored control is named.</summary>
    [Fact]
    public async Task HasNoControlToOperate()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var window = Show(new PrivacySettingsPage(), out var view);
            try
            {
                SettingsTree.Visible<Control>(view).OfType<Button>().ShouldBeEmpty();
                SettingsTree.Visible<Control>(view).OfType<TextBox>().ShouldBeEmpty();
                SettingsTree.Visible<Control>(view).OfType<CheckBox>().ShouldBeEmpty();
                SettingsTree.Unnamed(view).ShouldBeEmpty();
            }
            finally
            {
                window.Close();
            }
        }, ct).ConfigureAwait(true);
    }
}
