using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using Autonogrammer.Presentation.Settings;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>A check box on a Settings page draws its box and its label on one centre line: the 32 px target height must not leave the box above the text.</summary>
public sealed class SettingsCheckBoxAlignmentTests
{
    /// <summary>The Agents page check box: the vertical centre of the box and of the label differ by less than a pixel.</summary>
    [Fact]
    public async Task TheBoxAndTheLabelShareACentreLine()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var page = fixture.Show(SettingsSection.Agents);

            var box = page.GetVisualDescendants().OfType<CheckBox>().Single(static check => string.Equals(check.Name, "AutoRunBox", StringComparison.Ordinal));
            var square = box.GetVisualDescendants().OfType<Border>().Single(static border => string.Equals(border.Name, "NormalRectangle", StringComparison.Ordinal));
            var text = box.GetVisualDescendants().OfType<ContentPresenter>().Single(static presenter => presenter.Content is string).GetVisualDescendants().OfType<TextBlock>().Single();

            var boxCentre = CentreY(square, box);
            var textCentre = CentreY(text, box);

            Math.Abs(boxCentre - textCentre).ShouldBeLessThan(1.0, $"box centre {boxCentre}, label centre {textCentre}");
        }, ct).ConfigureAwait(true);
    }

    private static double CentreY(Visual visual, Visual relativeTo)
    {
        var top = visual.TranslatePoint(default, relativeTo)!.Value;
        return top.Y + (visual.Bounds.Height / 2);
    }
}
