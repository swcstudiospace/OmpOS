using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using OmpOS.Contracts.Agents;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation.AgentPanel;
using Shouldly;
using Xunit;
using Path = Avalonia.Controls.Shapes.Path;

namespace OmpOS.Native.Tests.AgentPanel.Views;

/// <summary>The glyph table, the status chip and the Running pulse (46-UI-SPEC, Glyph Table, Status chip and Motion).</summary>
public sealed class StatusGlyphTests
{
    /// <summary>Each glyph key draws path data and the stroke role the glyph table gives it.</summary>
    [Theory]
    [InlineData(StatusIcon.Ring, "tone-secondary")]
    [InlineData(StatusIcon.Clock, "tone-secondary")]
    [InlineData(StatusIcon.Running, "tone-accent")]
    [InlineData(StatusIcon.Alert, "tone-warning")]
    [InlineData(StatusIcon.Bubble, "tone-warning")]
    [InlineData(StatusIcon.Paused, "tone-secondary")]
    [InlineData(StatusIcon.Check, "tone-success")]
    [InlineData(StatusIcon.Cross, "tone-error")]
    [InlineData(StatusIcon.Question, "tone-warning")]
    [InlineData(StatusIcon.Closed, "tone-secondary")]
    [InlineData(StatusIcon.Slash, "tone-secondary")]
    [InlineData(StatusIcon.Info, "tone-secondary")]
    public async Task EachGlyphKeyDrawsItsPathInItsStrokeRole(string key, string expectedClass)
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var icon = new StageIcon { IconKey = key };
            var window = ViewHarness.Show(icon);

            var path = icon.GetVisualDescendants().OfType<Path>().Single();

            path.Data.ShouldNotBeNull();
            path.Classes.Contains(expectedClass).ShouldBeTrue($"{key} is stroked in {expectedClass}");
            path.StrokeThickness.ShouldBe(1.5);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A glyph key this build does not know draws the info glyph and never throws.</summary>
    [Fact]
    public async Task AnUnknownKeyDrawsTheInfoGlyph()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var icon = new StageIcon { IconKey = "from-the-future" };
            var window = ViewHarness.Show(icon);

            var path = icon.GetVisualDescendants().OfType<Path>().Single();

            path.Data.ShouldNotBeNull();
            path.Classes.Contains("tone-secondary").ShouldBeTrue();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A glyph is decorative: it is raw to assistive technology and never a tab stop.</summary>
    [Fact]
    public async Task AGlyphIsDecorative()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var icon = new StageIcon { IconKey = StatusIcon.Check };
            var window = ViewHarness.Show(icon);

            AutomationProperties.GetAccessibilityView(icon).ShouldBe(AccessibilityView.Raw);
            icon.Focusable.ShouldBeFalse();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The Running glyph pulses under full motion.</summary>
    [Fact]
    public async Task TheRunningGlyphPulsesUnderFullMotion()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var icon = new StageIcon { IconKey = StatusIcon.Running };
            var window = ViewHarness.Show(icon, reducedMotion: false);

            icon.IsPulsing.ShouldBeTrue();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Under the reduced-motion class no glyph carries the pulse class, and a glyph moved under it stops pulsing.</summary>
    [Fact]
    public async Task NoGlyphPulsesUnderReducedMotion()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var icon = new StageIcon { IconKey = StatusIcon.Running };
            var window = ViewHarness.Show(icon, reducedMotion: true);

            icon.IsPulsing.ShouldBeFalse("the glyph is static under reduced motion");
            window.Close();

            var other = new StageIcon { IconKey = StatusIcon.Running };
            var host = new Border { Child = other };
            host.Classes.Add(ViewHarness.ReducedMotionClass);
            var second = ViewHarness.Show(host);

            other.IsPulsing.ShouldBeFalse("any ancestor with the class is enough");
            second.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Only the Running glyph pulses.</summary>
    [Fact]
    public async Task OnlyTheRunningGlyphPulses()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var icon = new StageIcon { IconKey = StatusIcon.Check };
            var window = ViewHarness.Show(icon);

            icon.IsPulsing.ShouldBeFalse();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The chip shows the words next to the glyph on a 24 px chip, and colours the words by role.</summary>
    [Fact]
    public async Task TheChipShowsWordsAndGlyphInItsRole()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var chip = new StatusChip { Text = "Needs approval", IconKey = StatusIcon.Alert, Tone = StatusTone.Warning };
            var window = ViewHarness.Show(chip);

            var label = ViewHarness.Find<TextBlock>(chip, "Label");
            var frame = ViewHarness.Find<Border>(chip, "Frame");

            label.Text.ShouldBe("Needs approval");
            label.Classes.Contains("tone-warning").ShouldBeTrue();
            label.Classes.Contains("tone-secondary").ShouldBeFalse("only the current role is applied");
            frame.Height.ShouldBe(20);
            frame.BorderThickness.Left.ShouldBe(1);
            chip.GetVisualDescendants().OfType<StageIcon>().Single().IconKey.ShouldBe(StatusIcon.Alert);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Row and stage statuses map to the text roles of the status table.</summary>
    [Fact]
    public void StatusesMapToTheirTextRoles()
    {
        StatusGlyphs.ToneOf(AgentRowStatus.NeedsApproval).ShouldBe(StatusTone.Warning);
        StatusGlyphs.ToneOf(AgentRowStatus.WaitingForYou).ShouldBe(StatusTone.Warning);
        StatusGlyphs.ToneOf(AgentRowStatus.Lost).ShouldBe(StatusTone.Warning);
        StatusGlyphs.ToneOf(AgentRowStatus.Running).ShouldBe(StatusTone.Primary);
        StatusGlyphs.ToneOf(AgentRowStatus.Queued).ShouldBe(StatusTone.Primary);
        StatusGlyphs.ToneOf(AgentRowStatus.Ready).ShouldBe(StatusTone.Secondary);
        StatusGlyphs.ToneOf(AgentRowStatus.Closed).ShouldBe(StatusTone.Secondary);
        StatusGlyphs.ToneOf(AgentRowStatus.Completed).ShouldBe(StatusTone.Success);
        StatusGlyphs.ToneOf(AgentRowStatus.Failed).ShouldBe(StatusTone.Error);
        StatusGlyphs.ToneOf(AgentRowStatus.Unknown).ShouldBe(StatusTone.Secondary);
        StatusGlyphs.ToneOf(StageStatus.Succeeded).ShouldBe(StatusTone.Success);
        StatusGlyphs.ToneOf(StageStatus.Degraded).ShouldBe(StatusTone.Warning);
        StatusGlyphs.ToneOf(StageStatus.Failed).ShouldBe(StatusTone.Error);
        StatusGlyphs.ToneOf((StageStatus)99).ShouldBe(StatusTone.Secondary);
    }

    /// <summary>Every key the presentation layer can name has a glyph in the table, so no status draws the fallback by accident.</summary>
    [Fact]
    public void EveryStatusIconKeyOfTheModelsHasAGlyph()
    {
        foreach (var status in Enum.GetValues<AgentRowStatus>())
        {
            StatusGlyphs.IsKnown(AgentRowStatusMap.IconOf(status)).ShouldBeTrue($"{status} names a glyph the table draws");
        }

        foreach (var status in Enum.GetValues<StageStatus>())
        {
            StatusGlyphs.IsKnown(AgentPanelCopy.StageStatusIcon(status)).ShouldBeTrue($"{status} names a glyph the table draws");
        }
    }
}
