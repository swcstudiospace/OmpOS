using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Kanban;
using Autonogrammer.Presentation.Pets;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Pets;

/// <summary>
/// The companion state model (Phase 41, criteria 1 and 2): runtime and row statuses map to the recorded pet state, every state has text
/// for screen readers, reduced motion turns animation off, the hide toggle removes the companion, and a status this build does not know
/// is shown as the fallback state rather than as a calm one.
/// </summary>
public sealed class PetStateModelTests
{
    /// <summary>The recorded table, one row per runtime status. A status added to the enum without a row here fails the completeness check.</summary>
    private static readonly Dictionary<RuntimeStatus, PetState> RuntimeTable = new()
    {
        [RuntimeStatus.Queued] = PetState.Working,
        [RuntimeStatus.Running] = PetState.Working,
        [RuntimeStatus.AwaitingInput] = PetState.AttentionNeeded,
        [RuntimeStatus.NeedsAttention] = PetState.AttentionNeeded,
        [RuntimeStatus.Paused] = PetState.Idle,
        [RuntimeStatus.Completed] = PetState.Idle,
        [RuntimeStatus.Failed] = PetState.AttentionNeeded,
        [RuntimeStatus.Unreachable] = PetState.Fallback,
    };

    /// <summary>The recorded table, one row per panel row status.</summary>
    private static readonly Dictionary<AgentRowStatus, PetState> RowTable = new()
    {
        [AgentRowStatus.Unknown] = PetState.Fallback,
        [AgentRowStatus.NeedsApproval] = PetState.AttentionNeeded,
        [AgentRowStatus.WaitingForYou] = PetState.AttentionNeeded,
        [AgentRowStatus.Running] = PetState.Working,
        [AgentRowStatus.Queued] = PetState.Working,
        [AgentRowStatus.Lost] = PetState.Fallback,
        [AgentRowStatus.Ready] = PetState.Idle,
        [AgentRowStatus.Paused] = PetState.Idle,
        [AgentRowStatus.Closed] = PetState.Idle,
        [AgentRowStatus.Completed] = PetState.Idle,
        [AgentRowStatus.Failed] = PetState.AttentionNeeded,
    };

    /// <summary>Each runtime status and each panel row status maps to the state recorded for it, and the tables cover every defined value.</summary>
    [Fact]
    public void RuntimeStatusMapsToThePetState()
    {
        RuntimeTable.Count.ShouldBe(Enum.GetValues<RuntimeStatus>().Length);
        RowTable.Count.ShouldBe(Enum.GetValues<AgentRowStatus>().Length);

        foreach (var row in RuntimeTable)
        {
            PetStateModel.StateOf(row.Key).ShouldBe(row.Value, $"runtime status {row.Key}");
        }

        foreach (var row in RowTable)
        {
            PetStateModel.StateOf(row.Key).ShouldBe(row.Value, $"row status {row.Key}");
        }

        PetStateModel.StateOf((RuntimeStatus?)null).ShouldBe(PetState.Idle);
    }

    /// <summary>The model applies a status through its setters and exposes the mapped state.</summary>
    [Fact]
    public void ASetStatusDrivesTheStateAndAnnouncesIt()
    {
        var model = new PetStateModel();
        var changed = PropertyChangeRecorder.Record(model);

        model.SetRuntimeStatus(RuntimeStatus.NeedsAttention);

        model.State.ShouldBe(PetState.AttentionNeeded);
        model.AccessibleText.ShouldBe(PetAccessibleText.TextOf(PetState.AttentionNeeded));
        changed.ShouldAnnounce(nameof(PetStateModel.State));
        changed.ShouldAnnounce(nameof(PetStateModel.AccessibleText));

        changed.Clear();
        model.SetRuntimeStatus(RuntimeStatus.Failed);

        changed.ShouldBeEmpty("the state did not change, so nothing is announced");
    }

    /// <summary>Several sessions show the most urgent state: attention, then working, then fallback, then idle.</summary>
    [Fact]
    public void SeveralSessionsShowTheMostUrgentState()
    {
        var model = new PetStateModel();

        model.SetRowStatuses([AgentRowStatus.Completed, AgentRowStatus.Lost]);
        model.State.ShouldBe(PetState.Fallback);

        model.SetRowStatuses([AgentRowStatus.Completed, AgentRowStatus.Lost, AgentRowStatus.Running]);
        model.State.ShouldBe(PetState.Working);

        model.SetRowStatuses([AgentRowStatus.Running, AgentRowStatus.NeedsApproval, AgentRowStatus.Lost]);
        model.State.ShouldBe(PetState.AttentionNeeded);

        model.SetRowStatuses([]);
        model.State.ShouldBe(PetState.Idle);
    }

    /// <summary>Every state, hidden included, has its own non-blank sentence; the sentences differ so a screen reader user can tell the states apart.</summary>
    [Fact]
    public void EveryPetStateHasTextForScreenReaders()
    {
        var texts = Enum.GetValues<PetState>().Select(PetAccessibleText.TextOf).ToArray();

        texts.ShouldAllBe(text => !string.IsNullOrWhiteSpace(text));
        texts.Distinct(StringComparer.Ordinal).Count().ShouldBe(texts.Length);
        PetAccessibleText.ToggleLabelOf(isHidden: true).ShouldBe("Show companion");
        PetAccessibleText.ToggleLabelOf(isHidden: false).ShouldBe("Hide companion");
    }

    /// <summary>Reduced motion, chosen by the user or asked by the system, turns animation off; full motion overrides the system flag.</summary>
    [Fact]
    public void ReducedMotionDisablesAnimation()
    {
        var model = new PetStateModel();
        model.SetRuntimeStatus(RuntimeStatus.Running);

        model.AnimationEnabled.ShouldBeTrue();

        model.Preferences = new PetPreferences(Motion: MotionPreference.Reduced);
        model.AnimationEnabled.ShouldBeFalse();
        model.IsVisible.ShouldBeTrue("reduced motion keeps the companion on screen, only still");
        model.State.ShouldBe(PetState.Working);

        model.Preferences = new PetPreferences(Motion: MotionPreference.System);
        model.AnimationEnabled.ShouldBeTrue();

        model.SystemPrefersReducedMotion = true;
        model.AnimationEnabled.ShouldBeFalse();

        model.Preferences = new PetPreferences(Motion: MotionPreference.Full);
        model.AnimationEnabled.ShouldBeTrue();
    }

    /// <summary>The hide toggle removes the companion from the layout, stops its animation, and brings it back unchanged.</summary>
    [Fact]
    public void HideToggleRemovesTheCompanion()
    {
        var model = new PetStateModel();
        model.SetRuntimeStatus(RuntimeStatus.AwaitingInput);
        var changed = PropertyChangeRecorder.Record(model);

        model.ToggleHidden();

        model.State.ShouldBe(PetState.Hidden);
        model.IsVisible.ShouldBeFalse();
        model.AnimationEnabled.ShouldBeFalse();
        model.AccessibleText.ShouldBe(PetAccessibleText.TextOf(PetState.Hidden));
        model.ToggleLabel.ShouldBe("Show companion");
        model.MappedState.ShouldBe(PetState.AttentionNeeded, "hiding does not forget the session state");
        changed.ShouldAnnounce(nameof(PetStateModel.IsVisible));

        model.SetRuntimeStatus(RuntimeStatus.Running);
        model.State.ShouldBe(PetState.Hidden, "a status change never brings a hidden companion back");

        model.ToggleHidden();

        model.State.ShouldBe(PetState.Working);
        model.IsVisible.ShouldBeTrue();
        model.ToggleLabel.ShouldBe("Hide companion");
    }

    /// <summary>A status or state outside the enums falls back; an unreachable session also reads as fallback, never as idle.</summary>
    [Fact]
    public void UnmappedStatusFallsBackToTheFallbackState()
    {
        PetStateModel.StateOf((RuntimeStatus?)(RuntimeStatus)999).ShouldBe(PetState.Fallback);
        PetStateModel.StateOf((AgentRowStatus)999).ShouldBe(PetState.Fallback);
        PetAccessibleText.TextOf((PetState)999).ShouldBe(PetAccessibleText.TextOf(PetState.Fallback));
        new PetPreferences(Motion: (MotionPreference)999).IsMotionReduced(systemPrefersReducedMotion: false).ShouldBeTrue();

        var model = new PetStateModel();
        model.SetRuntimeStatus((RuntimeStatus)999);

        model.State.ShouldBe(PetState.Fallback);
    }

    /// <summary>A null preference is refused rather than stored.</summary>
    [Fact]
    public void ANullPreferenceIsRefused()
    {
        var model = new PetStateModel();

        Should.Throw<ArgumentNullException>(() => model.Preferences = null!);
    }
}
