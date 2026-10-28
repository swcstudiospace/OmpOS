using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Commands;
using OmpOS.Presentation.Composer;
using OmpOS.Presentation.Runs;
using OmpOS.UI.Components;
using OmpOS.UI.Tests.Fakes;
using Shouldly;
using Xunit;

namespace OmpOS.UI.Tests;

/// <summary>The composer of an Agent Panel session: the model, mode and thinking chips, the slash palette as a listbox, and steering while a run is active.</summary>
public sealed class ComposerPanelTests : UiTestContext
{
    private const string KeysModule = "./_content/OmpOS.UI/scripts/ompos-keys.js";
    private const string FocusIdentifier = "Blazor._internal.domWrapper.focus";

    private static readonly OptionChoice DefaultMode = OptionChoice.Create("default", "Default", null);
    private static readonly OptionChoice PlanMode = OptionChoice.Create("plan", "Plan", "Read-only planning mode");

    private static ComposerViewModel NewModel(List<string>? sent = null) =>
        new(
            text =>
            {
                sent?.Add(text);
                return Task.FromResult(true);
            },
            () => Task.CompletedTask);

    private static OptionChipModel ModeChip(List<string> requests)
    {
        var chip = new OptionChipModel("mode", "Mode", (value, _) =>
        {
            requests.Add(value);
            return Task.FromResult(OptionChangeResult.Set(value));
        });
        chip.Refresh([DefaultMode, PlanMode], "default");
        return chip;
    }

    private static SlashCommandDescriptor Command(string name, string? disabledReason = null) => new()
    {
        Name = name,
        Description = $"Runs {name}.",
        Platforms = CommandPlatforms.Linux | CommandPlatforms.Windows | CommandPlatforms.MacOS,
        DisabledReason = disabledReason,
        HandlerId = $"test.{name}",
    };

    private IRenderedComponent<Composer> RenderComposer(ComposerViewModel model, SteeringInputModel? steering = null, Action? onStop = null) =>
        Render<Composer>(parameters =>
        {
            parameters.Add(p => p.Model, model);
            if (steering is not null)
            {
                parameters.Add(p => p.Steering, steering);
            }

            parameters.Add(p => p.OnStop, () => onStop?.Invoke());
        });

    private static (RunInputScheduler Scheduler, PanelRunInput Input, SteeringInputModel Steering) Steering(Func<string, SteeringMode, CancellationToken, Task<SteeringDelivery>>? live = null)
    {
        var input = new PanelRunInput();
        var scheduler = new RunInputScheduler(input);
        return (scheduler, input, new SteeringInputModel(scheduler, TimeProvider.System, recorder: null, liveDelivery: live));
    }

    // Chips -----------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The mode chip names itself by what it changes and its value, and opens a listbox that marks the current choice.</summary>
    [Fact]
    public async Task TheModeChipOpensAListboxWithTheCurrentChoiceSelected()
    {
        var model = NewModel();
        model.ModeChip = ModeChip([]);
        var cut = RenderComposer(model);
        var chip = cut.Find("button.ompos-chip-btn");

        chip.GetAttribute("aria-label").ShouldBe("Mode: Default");
        chip.GetAttribute("aria-haspopup").ShouldBe("listbox");
        chip.GetAttribute("aria-expanded").ShouldBe("false");

        await chip.ClickAsync().ConfigureAwait(true);

        cut.Find("button.ompos-chip-btn").GetAttribute("aria-expanded").ShouldBe("true");
        var options = cut.FindAll("ul[role='listbox'] li[role='option']");
        options.Select(static option => option.GetAttribute("aria-selected")).ShouldBe(["true", "false"]);
        cut.Find("ul[role='listbox']").GetAttribute("aria-label").ShouldBe("Mode");
    }

    /// <summary>The arrow keys move the highlight through the choices, Enter chooses, and the chip shows the new value.</summary>
    [Fact]
    public async Task ArrowKeysAndEnterChooseAMode()
    {
        var requests = new List<string>();
        var model = NewModel();
        model.ModeChip = ModeChip(requests);
        var cut = RenderComposer(model);
        await cut.Find("button.ompos-chip-btn").ClickAsync().ConfigureAwait(true);
        var first = cut.Find("ul[role='listbox']").GetAttribute("aria-activedescendant");

        await cut.Find("ul[role='listbox']").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" }).ConfigureAwait(true);
        string.Equals(cut.Find("ul[role='listbox']").GetAttribute("aria-activedescendant"), first, StringComparison.Ordinal).ShouldBeFalse();
        await cut.Find("ul[role='listbox']").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }).ConfigureAwait(true);

        requests.ShouldBe(["plan"]);
        await cut.WaitForAssertionAsync(() => cut.Find("button.ompos-chip-btn").GetAttribute("aria-label").ShouldBe("Mode: Plan")).ConfigureAwait(true);
        cut.FindAll("ul[role='listbox']").ShouldBeEmpty();
    }

    /// <summary>Escape closes the list without choosing and returns the focus to the chip.</summary>
    [Fact]
    public async Task EscapeClosesTheListAndReturnsTheFocus()
    {
        var requests = new List<string>();
        var model = NewModel();
        model.ModeChip = ModeChip(requests);
        var cut = RenderComposer(model);
        var chipId = cut.Find("button.ompos-chip-btn").InitialReferenceId();
        await cut.Find("button.ompos-chip-btn").ClickAsync().ConfigureAwait(true);

        await cut.Find("ul[role='listbox']").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" }).ConfigureAwait(true);

        cut.FindAll("ul[role='listbox']").ShouldBeEmpty();
        requests.ShouldBeEmpty();
        JSInterop.Invocations[FocusIdentifier][^1].ReferenceIdOf().ShouldBe(chipId);
    }

    /// <summary>While a run is active the chip is disabled with its reason as text, and it does not open.</summary>
    [Fact]
    public async Task AChipIsDisabledWhileARunIsActive()
    {
        var model = NewModel();
        model.ModeChip = ModeChip([]);
        model.IsStreaming = true;
        var cut = RenderComposer(model);

        var chip = cut.Find("button.ompos-chip-btn");
        chip.GetAttribute("aria-disabled").ShouldBe("true");
        cut.Find(".ompos-sr-only").TextContent.ShouldBe(model.ModeChip.ToolTipText);
        await chip.ClickAsync().ConfigureAwait(true);

        cut.FindAll("ul[role='listbox']").ShouldBeEmpty();
    }

    /// <summary>The model picker is a combobox with a search box over a listbox; typing filters, Enter switches.</summary>
    [Fact]
    public async Task TheModelPickerSearchesAndSwitches()
    {
        var requests = new List<string>();
        var picker = new ModelPickerModel((value, _) =>
        {
            requests.Add(value);
            return Task.FromResult(ModelSwitchResult.Switched(value));
        });
        picker.Refresh(
            [
                ModelChoice.Create("anthropic/claude-sonnet", "Claude Sonnet"),
                ModelChoice.Create("anthropic/claude-opus", "Claude Opus"),
                ModelChoice.Create("openai/gpt-x", "GPT X"),
            ],
            "anthropic/claude-sonnet");
        var model = NewModel();
        model.ModelPicker = picker;
        var cut = RenderComposer(model);
        var chip = cut.Find("button.ompos-chip-btn");
        chip.GetAttribute("aria-label").ShouldBe("Model: " + picker.Label);

        await chip.ClickAsync().ConfigureAwait(true);
        var search = cut.Find("input[role='combobox']");
        search.GetAttribute("aria-label").ShouldBe("Search models");
        search.GetAttribute("aria-controls").ShouldBe(cut.Find("ul[role='listbox']").Id);
        cut.FindAll("li[role='option']").Count.ShouldBe(3);

        await search.InputAsync(new ChangeEventArgs { Value = "opus" }).ConfigureAwait(true);
        cut.FindAll("li[role='option']").Count.ShouldBe(1);
        await cut.Find("input[role='combobox']").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }).ConfigureAwait(true);

        requests.ShouldBe(["anthropic/claude-opus"]);
        cut.FindAll("input[role='combobox']").ShouldBeEmpty();
    }

    /// <summary>A filter no model matches says so in a status line.</summary>
    [Fact]
    public async Task AModelFilterWithNoMatchSaysSo()
    {
        var picker = new ModelPickerModel((value, _) => Task.FromResult(ModelSwitchResult.Switched(value)));
        picker.Refresh([ModelChoice.Create("openai/gpt-x", "GPT X")], "openai/gpt-x");
        var model = NewModel();
        model.ModelPicker = picker;
        var cut = RenderComposer(model);
        await cut.Find("button.ompos-chip-btn").ClickAsync().ConfigureAwait(true);

        await cut.Find("input[role='combobox']").InputAsync(new ChangeEventArgs { Value = "zzz" }).ConfigureAwait(true);

        cut.Find(".ompos-model-state[role='status']").TextContent.ShouldContain("No model matches", Case.Sensitive);
    }

    // Slash palette ---------------------------------------------------------------------------------------------------------------------------------

    private static (ComposerViewModel Model, SlashPaletteModel Palette) PaletteModel(List<string>? sent = null)
    {
        var model = NewModel(sent);
        var palette = new SlashPaletteModel(new SlashCommandRegistry([Command("clear"), Command("fork", "Needs a completed prompt; run one first.")]));
        model.SlashPalette = palette;
        return (model, palette);
    }

    /// <summary>Typing a slash opens the palette as a listbox the textarea controls, with a source badge per row and the result count in a polite status.</summary>
    [Fact]
    public async Task ASlashOpensTheListboxTheTextareaControls()
    {
        var (model, palette) = PaletteModel();
        var cut = RenderComposer(model);
        cut.Find("textarea").GetAttribute("aria-controls").ShouldBeNull();

        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = "/" }).ConfigureAwait(true);

        var list = cut.Find("ul[role='listbox'].ompos-slash-list");
        var textarea = cut.Find("textarea");
        textarea.GetAttribute("aria-controls").ShouldBe(list.Id);
        textarea.GetAttribute("aria-autocomplete").ShouldBe("list");
        textarea.GetAttribute("data-slash-open").ShouldBe("true");
        var options = cut.FindAll("li[role='option']");
        options.Count.ShouldBe(palette.Entries.Count);
        textarea.GetAttribute("aria-activedescendant").ShouldBe(options[0].Id);
        for (var index = 0; index < options.Count; index++)
        {
            options[index].GetAttribute("aria-label").ShouldBe(palette.Entries[index].AutomationName);
            options[index].QuerySelector(".ompos-badge-source")!.TextContent.ShouldBe(palette.Entries[index].SourceLabel);
        }

        var status = cut.Find(".ompos-slash-host > [role='status']");
        status.GetAttribute("aria-live").ShouldBe("polite");
        status.TextContent.ShouldBe(palette.Announcement);
    }

    /// <summary>Arrow keys move the selection, and Enter puts the command in the draft without sending it and closes the list.</summary>
    [Fact]
    public async Task EnterAcceptsTheHighlightedCommandWithoutSending()
    {
        var sent = new List<string>();
        var (model, _) = PaletteModel(sent);
        var cut = RenderComposer(model);
        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = "/" }).ConfigureAwait(true);

        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }).ConfigureAwait(true);

        model.Draft.ShouldBe("/clear ");
        sent.ShouldBeEmpty();
        await cut.WaitForAssertionAsync(() => cut.FindAll("ul.ompos-slash-list").ShouldBeEmpty()).ConfigureAwait(true);
    }

    /// <summary>A disabled command shows its reason and is not put in the draft.</summary>
    [Fact]
    public async Task ADisabledCommandShowsItsReasonAndIsNotAccepted()
    {
        var (model, palette) = PaletteModel();
        var cut = RenderComposer(model);
        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = "/" }).ConfigureAwait(true);
        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" }).ConfigureAwait(true);
        cut.Find("textarea").GetAttribute("aria-activedescendant").ShouldBe(cut.FindAll("li[role='option']")[1].Id);

        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }).ConfigureAwait(true);

        model.Draft.ShouldBe("/");
        cut.FindAll("li[role='option']")[1].GetAttribute("aria-disabled").ShouldBe("true");
        cut.Find(".ompos-slash-message").TextContent.ShouldBe(palette.Notice);
        cut.Find(".ompos-slash-reason").TextContent.ShouldBe("Needs a completed prompt; run one first.");
    }

    /// <summary>Escape closes the palette and a click on a row accepts that row.</summary>
    [Fact]
    public async Task EscapeClosesAndAClickAccepts()
    {
        var (model, _) = PaletteModel();
        var cut = RenderComposer(model);
        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = "/" }).ConfigureAwait(true);

        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" }).ConfigureAwait(true);
        await cut.WaitForAssertionAsync(() => cut.FindAll("ul.ompos-slash-list").ShouldBeEmpty()).ConfigureAwait(true);

        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = "/c" }).ConfigureAwait(true);
        await cut.Find("li[role='option']").ClickAsync().ConfigureAwait(true);

        model.Draft.ShouldBe("/clear ");
    }

    // Steering --------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>With no run the primary control is Send; while a run is active it becomes Send now, with Queue and Stop beside it.</summary>
    [Fact]
    public void WhileARunIsActiveTheControlsAreSendNowQueueAndStop()
    {
        var (scheduler, _, steering) = Steering();
        var cut = RenderComposer(NewModel(), steering);
        cut.FindAll("button[aria-label='Send message']").Count.ShouldBe(1);
        cut.FindAll("button.ompos-send-now").ShouldBeEmpty();
        cut.Find("textarea").GetAttribute("placeholder").ShouldBe(AgentPanelCopy.ComposerPlaceholderIdle);

        scheduler.RunStarted();

        cut.WaitForAssertion(() => cut.Find("button.ompos-send-now").TextContent.ShouldBe(AgentPanelCopy.SendNow));
        cut.Find("button.ompos-send-now").GetAttribute("aria-label").ShouldBe(AgentPanelViewText.SteerName);
        cut.FindAll("button").Single(static button => string.Equals(button.TextContent, AgentPanelCopy.QueueFollowUpLabel, StringComparison.Ordinal)).GetAttribute("aria-label").ShouldBe(AgentPanelViewText.QueueFollowUpName);
        cut.FindAll("button[aria-label='Stop response']").Count.ShouldBe(1);
        cut.FindAll("button[aria-label='Send message']").ShouldBeEmpty();
        cut.Find("textarea").GetAttribute("placeholder").ShouldBe(AgentPanelCopy.ComposerPlaceholderRunning);
    }

    /// <summary>Enter steers the active run, clears the draft and lists the message in the steering history with its delivery.</summary>
    [Fact]
    public async Task EnterSteersTheRunAndTheHistoryListsIt()
    {
        var (scheduler, input, steering) = Steering();
        var model = NewModel();
        var cut = RenderComposer(model, steering);
        scheduler.RunStarted();
        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = "go left" }).ConfigureAwait(true);

        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }).ConfigureAwait(true);

        input.Steers.ShouldBe(["go left"]);
        model.Draft.ShouldBeEmpty();
        await cut.WaitForAssertionAsync(() =>
        {
            cut.Find("details.ompos-steering-details summary").TextContent.ShouldBe("Steering history (1)");
            var entry = cut.Find("li.ompos-steering-entry");
            entry.TextContent.ShouldContain("go left", Case.Sensitive);
            entry.TextContent.ShouldContain(steering.History[0].StatusLine, Case.Sensitive);
        }).ConfigureAwait(true);
    }

    /// <summary>The Queue control and Ctrl+Shift+Enter queue a follow-up, and the pending line says how many wait.</summary>
    [Fact]
    public async Task QueueAndCtrlShiftEnterQueueFollowUps()
    {
        var (scheduler, _, steering) = Steering();
        var model = NewModel();
        var cut = RenderComposer(model, steering);
        scheduler.RunStarted();
        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = "then run tests" }).ConfigureAwait(true);

        await cut.FindAll("button").Single(static button => string.Equals(button.TextContent, AgentPanelCopy.QueueFollowUpLabel, StringComparison.Ordinal)).ClickAsync().ConfigureAwait(true);
        steering.QueuedFollowUpCount.ShouldBe(1);
        await cut.WaitForAssertionAsync(() => cut.Find(".ompos-composer-pending").TextContent.ShouldContain(AgentPanelViewText.QueuedOne, Case.Sensitive)).ConfigureAwait(true);

        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = "then lint" }).ConfigureAwait(true);
        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter", CtrlKey = true, ShiftKey = true }).ConfigureAwait(true);

        steering.QueuedFollowUpCount.ShouldBe(2);
        await cut.WaitForAssertionAsync(() => cut.Find(".ompos-composer-pending").TextContent.ShouldContain(AgentPanelViewText.QueuedMany(2), Case.Sensitive)).ConfigureAwait(true);
        cut.Find(".ompos-composer-pending").GetAttribute("aria-live").ShouldBe("polite");
    }

    /// <summary>A full queue keeps the draft and says why in an alert.</summary>
    [Fact]
    public async Task AFullQueueKeepsTheDraftAndSaysWhy()
    {
        var (scheduler, _, steering) = Steering();
        var model = NewModel();
        var cut = RenderComposer(model, steering);
        scheduler.RunStarted();
        for (var index = 0; index < RunInputScheduler.MaxQueuedMessages; index++)
        {
            (await steering.SubmitAsync("queued " + index, SteeringMode.FollowUp, Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe(SteeringSubmitOutcome.Accepted);
        }

        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = "one more" }).ConfigureAwait(true);
        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }).ConfigureAwait(true);

        model.Draft.ShouldBe("one more");
        cut.Find(".ompos-composer-status[role='alert']").TextContent.ShouldBe(AgentPanelCopy.QueueFull(RunInputScheduler.MaxQueuedMessages));
    }

    /// <summary>A live channel that cannot deliver keeps the draft, says the message was not delivered and lists it as failed.</summary>
    [Fact]
    public async Task AFailedLiveDeliveryKeepsTheDraft()
    {
        var (scheduler, _, steering) = Steering((_, _, _) => Task.FromResult(SteeringDelivery.Failed("omp is not reachable")));
        var model = NewModel();
        var cut = RenderComposer(model, steering);
        scheduler.RunStarted();
        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = "hello" }).ConfigureAwait(true);

        await cut.Find("button.ompos-send-now").ClickAsync().ConfigureAwait(true);

        model.Draft.ShouldBe("hello");
        cut.Find(".ompos-composer-status[role='alert']").TextContent.ShouldBe(AgentPanelCopy.DeliveryNotDelivered + ".");
        cut.Find("button.ompos-send-now").GetAttribute("aria-label").ShouldBe(AgentPanelCopy.ComposerSendNowName);
        cut.Find("li.ompos-steering-entry").GetAttribute("data-delivery").ShouldBe("failed");
    }

    /// <summary>Escape asks to stop while a run is active, and Shift+Enter is left to the text control.</summary>
    [Fact]
    public async Task EscapeStopsAndShiftEnterIsANewline()
    {
        var stops = 0;
        var (scheduler, input, steering) = Steering();
        var model = NewModel();
        var cut = RenderComposer(model, steering, () => stops++);
        scheduler.RunStarted();
        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = "draft" }).ConfigureAwait(true);

        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Enter", ShiftKey = true }).ConfigureAwait(true);
        await cut.Find("textarea").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" }).ConfigureAwait(true);

        input.Steers.ShouldBeEmpty();
        model.Draft.ShouldBe("draft");
        stops.ShouldBe(1);
    }

    /// <summary>A steering message beyond the history cap shows a count that says what is kept.</summary>
    [Fact]
    public async Task ALongSteeringDraftShowsACharacterCount()
    {
        var (scheduler, _, steering) = Steering();
        var model = NewModel();
        var cut = RenderComposer(model, steering);
        scheduler.RunStarted();

        await cut.Find("textarea").InputAsync(new ChangeEventArgs { Value = new string('x', SteeringInputModel.MaxTextChars + 1) }).ConfigureAwait(true);

        cut.Find(".ompos-composer-count[role='status']").TextContent.ShouldBe($"{SteeringInputModel.MaxTextChars + 1} characters. The steering history keeps the first {SteeringInputModel.MaxTextChars}.");
    }

    // Accessibility ---------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The textarea keeps its name, is described by the hint and registers the keyboard guard; every control has a name.</summary>
    [Fact]
    public void TheComposerIsNamedDescribedAndGuarded()
    {
        var keys = JSInterop.SetupModule(KeysModule);
        var model = NewModel();
        model.ModeChip = ModeChip([]);
        var cut = RenderComposer(model);

        var textarea = cut.Find("textarea");
        textarea.GetAttribute("aria-label").ShouldBe("Message");
        cut.Find("#" + textarea.GetAttribute("aria-describedby")).TextContent.ShouldBe(AgentPanelViewText.ComposerHint);
        keys.VerifyInvoke("guardComposer");
        foreach (var button in cut.FindAll("button"))
        {
            (button.GetAttribute("aria-label") ?? button.TextContent).Trim().ShouldNotBeEmpty();
        }

        cut.FindAll("script").ShouldBeEmpty();
        cut.FindAll("[style]").ShouldBeEmpty();
    }
}
