using OmpOS.Native.Views.Sessions;
using OmpOS.Presentation.Composer;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.SlashPalette;

/// <summary>The slash targets that a session's own composer provides: switching its mode chip, filtering and choosing in its model picker, and opening the palette.</summary>
public sealed class ComposerSlashTargetsTests
{
    private static ComposerViewModel Composer() => new(static _ => Task.FromResult(true), static () => Task.CompletedTask);

    private static OptionChipModel ModeChip(string current = "default", string? refusal = null)
    {
        var chip = new OptionChipModel(
            "mode",
            "Mode",
            (value, _) => Task.FromResult(refusal is null ? OptionChangeResult.Set(value) : OptionChangeResult.Refused(refusal, current)));
        chip.Refresh([OptionChoice.Create("default", "Default", null), OptionChoice.Create("plan", "Plan", null)], current);
        return chip;
    }

    private static ModelPickerModel Picker(params string[] values)
    {
        var picker = new ModelPickerModel((value, _) => Task.FromResult(ModelSwitchResult.Switched(value)));
        picker.Refresh([.. values.Select(static value => ModelChoice.Create(value, null))], values[0]);
        return picker;
    }

    /// <summary>The mode chip switches to the choice named by value or by name, ignoring case, and the target reports success with null.</summary>
    [Theory]
    [InlineData("plan")]
    [InlineData("PLAN")]
    [InlineData("Plan")]
    public async Task TheModeChipSwitchesToTheNamedMode(string mode)
    {
        var composer = Composer();
        composer.ModeChip = ModeChip();

        var failure = await ComposerSlashTargets.SetModeAsync(composer, mode, TestContext.Current.CancellationToken).ConfigureAwait(true);

        failure.ShouldBeNull();
        composer.ModeChip.CurrentValue.ShouldBe("plan", Case.Sensitive);
    }

    /// <summary>A mode the engine does not offer is reported with the modes it does offer; a session with no mode chip says it offers none.</summary>
    [Fact]
    public async Task AModeTheEngineDoesNotOfferIsReported()
    {
        var composer = Composer();
        var token = TestContext.Current.CancellationToken;

        (await ComposerSlashTargets.SetModeAsync(composer, "plan", token).ConfigureAwait(true)).ShouldBe("This session does not offer modes.", Case.Sensitive);

        composer.ModeChip = ModeChip();
        (await ComposerSlashTargets.SetModeAsync(composer, "ask", token).ConfigureAwait(true)).ShouldBe("The engine offers no ask mode. It offers: default, plan.", Case.Sensitive);
        composer.ModeChip.CurrentValue.ShouldBe("default", Case.Sensitive);
    }

    /// <summary>A mode change the engine refuses returns the engine's reason.</summary>
    [Fact]
    public async Task ARefusedModeChangeReturnsTheReason()
    {
        var composer = Composer();
        composer.ModeChip = ModeChip(refusal: "A turn is running.");

        var failure = await ComposerSlashTargets.SetModeAsync(composer, "plan", TestContext.Current.CancellationToken).ConfigureAwait(true);

        failure.ShouldBe("A turn is running.", Case.Sensitive);
        composer.ModeChip.CurrentValue.ShouldBe("default", Case.Sensitive);
    }

    /// <summary>The picker selects the model when exactly one matches the filter, and says so otherwise.</summary>
    [Fact]
    public async Task ThePickerSelectsASingleMatch()
    {
        var composer = Composer();
        composer.ModelPicker = Picker("anthropic/claude-sonnet", "anthropic/claude-opus", "openai/gpt-x");
        var token = TestContext.Current.CancellationToken;

        var one = await ComposerSlashTargets.PickModelAsync(composer, "opus", token).ConfigureAwait(true);
        var several = await ComposerSlashTargets.PickModelAsync(composer, "claude", token).ConfigureAwait(true);
        var none = await ComposerSlashTargets.PickModelAsync(composer, "llama", token).ConfigureAwait(true);

        one.ShouldBe("Model: anthropic/claude-opus.", Case.Sensitive);
        composer.ModelPicker.CurrentValue.ShouldBe("anthropic/claude-opus", Case.Sensitive);
        several.ShouldBe("Several models match 'claude': pick one in the model list.", Case.Sensitive);
        none.ShouldBe("No model matches 'llama'.", Case.Sensitive);
    }

    /// <summary>A session with no model picker says it offers none.</summary>
    [Fact]
    public async Task ASessionWithoutAPickerSaysSo()
    {
        var message = await ComposerSlashTargets.PickModelAsync(Composer(), "opus", TestContext.Current.CancellationToken).ConfigureAwait(true);

        message.ShouldBe("This session does not offer a model picker.", Case.Sensitive);
    }

    /// <summary>The combined targets keep the head's own, add the composer's, and help puts a slash and the typed text in the draft so the palette opens.</summary>
    [Fact]
    public async Task TheCombinedTargetsKeepTheHeadsAndAddTheComposers()
    {
        var composer = Composer();
        var headOpened = false;
        var head = new DesktopSlashTargets { OpenSettings = _ => headOpened = true, SetMode = (_, _) => Task.FromResult<string?>("head wins") };

        var targets = ComposerSlashTargets.Create(composer, head);
        targets.OpenSettings.ShouldNotBeNull()("rules").ShouldBeTrue();
        var mode = await targets.SetMode.ShouldNotBeNull()("plan", TestContext.Current.CancellationToken).ConfigureAwait(true);
        targets.ShowHelp.ShouldNotBeNull()("pl");

        headOpened.ShouldBeTrue();
        mode.ShouldBe("head wins", Case.Sensitive);
        targets.PickModel.ShouldNotBeNull();
        composer.Draft.ShouldBe("/pl", Case.Sensitive);
        ComposerSlashTargets.Create(composer, null).OpenSettings.ShouldBeNull();
        Should.Throw<ArgumentNullException>(() => ComposerSlashTargets.Create(null!, head));
    }
}
