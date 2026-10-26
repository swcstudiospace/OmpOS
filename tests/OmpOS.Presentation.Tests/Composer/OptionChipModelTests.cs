using OmpOS.Presentation.Composer;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Composer;

/// <summary>
/// The composer chip of a session option (mode, thinking level): it shows the value the engine uses, its tooltip carries the engine's own
/// description of that value, it is enabled only while no run is active, and a change the engine refuses leaves the value and sets a notice.
/// </summary>
public sealed class OptionChipModelTests
{
    private const string PlanDescription = "Read-only planning mode that drafts a plan to a markdown file before any code changes";

    private static readonly OptionChoice Default = OptionChoice.Create("default", "Default", null);
    private static readonly OptionChoice Plan = OptionChoice.Create("plan", "Plan", PlanDescription);

    private sealed class Host(Func<string, OptionChangeResult> answer)
    {
        public List<string> Requests { get; } = [];

        public Task<OptionChangeResult> SetAsync(string value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(value);
            return Task.FromResult(answer(value));
        }
    }

    private static Host Accepting() => new(value => OptionChangeResult.Set(value));

    private static OptionChipModel Chip(Host host, string current = "default")
    {
        var chip = new OptionChipModel("mode", "Mode", host.SetAsync);
        chip.Refresh([Default, Plan], current);
        return chip;
    }

    /// <summary>The chip names the value in use by its display name and offers the values the engine listed, in the engine's order.</summary>
    [Fact]
    public void TheChipShowsTheValueInUse()
    {
        var chip = Chip(Accepting(), "plan");

        chip.Label.ShouldBe("Plan");
        chip.SelectedChoice.ShouldBe(Plan);
        chip.CurrentValue.ShouldBe("plan");
        chip.Choices.ShouldBe([Default, Plan]);
        chip.OptionId.ShouldBe("mode");
        chip.IsVisible.ShouldBeTrue();
        chip.IsEnabled.ShouldBeTrue();
    }

    /// <summary>The tooltip of the plan mode is the engine's own description; a value with no description says what the chip does.</summary>
    [Fact]
    public void TheTooltipCarriesTheEnginesDescription()
    {
        var chip = Chip(Accepting(), "plan");

        chip.ToolTipText.ShouldBe("Mode: Plan. " + PlanDescription, Case.Sensitive);
        chip.CurrentDescription.ShouldBe(PlanDescription);

        chip.Refresh([Default, Plan], "default");

        chip.ToolTipText.ShouldBe("Mode: Default. Choose another.", Case.Sensitive);
        chip.CurrentDescription.ShouldBeNull();
    }

    /// <summary>A value the engine runs but does not list shows its raw identifier; before the engine reported a value the chip shows the option's own name.</summary>
    [Fact]
    public void AnUnlistedValueShowsItsIdentifier()
    {
        var chip = new OptionChipModel("mode", "Mode", Accepting().SetAsync);

        chip.Label.ShouldBe("Mode");
        chip.IsVisible.ShouldBeFalse("no values to choose from yet");

        chip.Refresh([Default, Plan], "mystery");

        chip.Label.ShouldBe("mystery");
        chip.SelectedChoice.ShouldBeNull();
    }

    /// <summary>Choosing another value asks the engine once, and the chip then shows the value the engine confirmed.</summary>
    [Fact]
    public async Task ChoosingAValueChangesItThroughTheEngine()
    {
        var host = Accepting();
        var chip = Chip(host);

        await chip.SelectAsync(Plan).ConfigureAwait(true);

        host.Requests.ShouldBe(["plan"]);
        chip.Label.ShouldBe("Plan");
        chip.Notice.ShouldBeNull();
        chip.IsChanging.ShouldBeFalse();
    }

    /// <summary>Choosing the value already in use asks nothing and clears an old notice.</summary>
    [Fact]
    public async Task ChoosingTheCurrentValueAsksNothing()
    {
        var host = new Host(_ => OptionChangeResult.Refused("No."));
        var chip = Chip(host);
        await chip.SelectAsync(Plan).ConfigureAwait(true);
        chip.Notice.ShouldBe("No.");
        host.Requests.Clear();

        await chip.SelectAsync(Default).ConfigureAwait(true);

        host.Requests.ShouldBeEmpty();
        chip.Notice.ShouldBeNull();
    }

    /// <summary>A refused change leaves the value as it was, sets the engine's sentence as the notice, and the engine's own answer wins when it names the value still in use.</summary>
    [Fact]
    public async Task ARefusedChangeKeepsTheValueAndSetsTheNotice()
    {
        var host = new Host(_ => OptionChangeResult.Refused("The setting cannot be changed while a response is running.", "default"));
        var chip = Chip(host);

        await chip.SelectAsync(Plan).ConfigureAwait(true);

        chip.CurrentValue.ShouldBe("default");
        chip.Label.ShouldBe("Default");
        chip.Notice.ShouldBe("The setting cannot be changed while a response is running.", Case.Sensitive);
        chip.HasNotice.ShouldBeTrue();
    }

    /// <summary>A refusal without a sentence still gets a plain one that names the option.</summary>
    [Fact]
    public async Task ARefusalWithoutAMessageGetsAPlainOne()
    {
        var chip = Chip(new Host(_ => new OptionChangeResult(false, null, null)));

        await chip.SelectAsync(Plan).ConfigureAwait(true);

        chip.Notice.ShouldBe("Mode could not be changed.", Case.Sensitive);
    }

    /// <summary>A run keeps the setting fixed: the chip is disabled, the tooltip says why, and nothing is asked; the end of the run enables it again.</summary>
    [Fact]
    public async Task AnActiveRunDisablesTheChip()
    {
        var host = Accepting();
        var chip = Chip(host);

        chip.IsRunActive = true;

        chip.IsEnabled.ShouldBeFalse();
        chip.ToolTipText.ShouldBe("This setting cannot be changed while a response is running.", Case.Sensitive);
        chip.SelectCommand.CanExecute(Plan).ShouldBeFalse();
        await chip.SelectAsync(Plan).ConfigureAwait(true);
        host.Requests.ShouldBeEmpty();

        chip.IsRunActive = false;

        chip.IsEnabled.ShouldBeTrue();
        chip.SelectCommand.CanExecute(Plan).ShouldBeTrue();
    }

    /// <summary>A terminal runs its own omp: the chip is hidden and disabled there.</summary>
    [Fact]
    public void ATerminalSessionHidesTheChip()
    {
        var chip = Chip(Accepting());

        chip.IsTerminalSession = true;

        chip.IsVisible.ShouldBeFalse();
        chip.IsEnabled.ShouldBeFalse();
    }

    /// <summary>Resetting forgets the values: the chip hides until the engine reports them again.</summary>
    [Fact]
    public void ResetHidesTheChip()
    {
        var chip = Chip(Accepting(), "plan");

        chip.Reset();

        chip.IsVisible.ShouldBeFalse();
        chip.CurrentValue.ShouldBeNull();
        chip.Choices.ShouldBeEmpty();
    }

    /// <summary>A fault of the host becomes the notice through the command, never an unobserved exception.</summary>
    [Fact]
    public async Task AFaultOfTheHostBecomesTheNotice()
    {
        var chip = new OptionChipModel("thinking", "Thinking", (_, _) => throw new InvalidOperationException("boom"));
        chip.Refresh([Default], "default");

        await chip.SelectCommand.ExecuteAsync(OptionChoice.Create("high", "high", null)).ConfigureAwait(true);

        chip.Notice.ShouldBe("Thinking could not be changed: boom", Case.Sensitive);
    }

    /// <summary>A value with a blank name falls back to its identifier, and a blank description to none.</summary>
    [Fact]
    public void ACreatedChoiceFallsBackToTheIdentifier()
    {
        var choice = OptionChoice.Create("high", "  ", " ");

        choice.Name.ShouldBe("high");
        choice.Description.ShouldBeNull();
        Should.Throw<ArgumentException>(() => OptionChoice.Create(" ", "x", null));
    }

    /// <summary>The chip tells its bindings when the label, the enablement and the notice change.</summary>
    [Fact]
    public async Task ChangesAreAnnounced()
    {
        var chip = Chip(new Host(_ => OptionChangeResult.Refused("No.")));
        var changed = new List<string?>();
        chip.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        chip.IsRunActive = true;
        chip.IsRunActive = false;
        await chip.SelectAsync(Plan).ConfigureAwait(true);
        chip.Refresh([Default, Plan], "plan");

        changed.ShouldContain(nameof(OptionChipModel.IsEnabled));
        changed.ShouldContain(nameof(OptionChipModel.ToolTipText));
        changed.ShouldContain(nameof(OptionChipModel.Notice));
        changed.ShouldContain(nameof(OptionChipModel.Label));
    }

    /// <summary>The composer hands its streaming state to the mode and thinking chips like it does to the model picker, also to a chip set while a response already streams.</summary>
    [Fact]
    public void TheComposerKeepsTheChipsInStepWithTheRun()
    {
        var composer = new ComposerViewModel(static _ => Task.FromResult(true), static () => Task.CompletedTask);
        var mode = Chip(Accepting());
        var thinking = new OptionChipModel("thinking", "Thinking", Accepting().SetAsync);
        thinking.Refresh([OptionChoice.Create("off", "Off", null)], "off");
        composer.ModeChip = mode;
        composer.IsStreaming = true;
        composer.ThinkingChip = thinking;

        mode.IsRunActive.ShouldBeTrue();
        thinking.IsRunActive.ShouldBeTrue("a chip set during a run starts disabled");

        composer.IsStreaming = false;

        mode.IsEnabled.ShouldBeTrue();
        thinking.IsEnabled.ShouldBeTrue();
        composer.ModeChip.ShouldBeSameAs(mode);
        composer.ThinkingChip.ShouldBeSameAs(thinking);
    }
}
