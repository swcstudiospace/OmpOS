using Autonogrammer.Presentation.Automations;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Automations;

/// <summary>
/// A write that fails with a defect, not with a source failure, ends the busy state and raises a change, so a bound view never keeps its
/// busy indicator on until the next action.
/// </summary>
public sealed class AutomationsWriteDefectRegressionTests
{
    /// <summary>The busy flag is cleared and the change is announced before the defect reaches the caller.</summary>
    [Fact]
    public async Task ADefectedWriteClearsBusyAndRaisesChange()
    {
        var model = new AutomationsViewModel(new DefectingAutomationsSource());
        var busyAtChange = new List<bool>();
        model.Changed += (_, _) => busyAtChange.Add(model.IsBusy);

        await Should.ThrowAsync<InvalidOperationException>(
            () => model.SetEnabledAsync("nightly", enabled: false, TestContext.Current.CancellationToken)).ConfigureAwait(true);

        model.IsBusy.ShouldBeFalse();
        busyAtChange.ShouldBe([false]);
    }

    /// <summary>A source whose enable write fails with a defect rather than a source failure.</summary>
    private sealed class DefectingAutomationsSource : IAutomationsSource
    {
        /// <inheritdoc />
        public Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public IReadOnlyList<AutomationEntry> Snapshot() => [];

        /// <inheritdoc />
        public Task SetEnabledAsync(string automationId, bool enabled, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The write defected.");

        /// <inheritdoc />
        public Task ResumeAsync(string automationId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
