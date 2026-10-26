using OmpOS.Harness.Abstractions;
using OmpOS.Native.Views.Sessions;
using OmpOS.Presentation.Composer;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.OptionChips;

/// <summary>The binding of one session option to a composer chip: the chip exists while omp reports the option, follows omp's own changes, and changes the option through omp.</summary>
public sealed class OptionChipBindingTests
{
    /// <summary>Until omp reports the option there is no chip; the report creates it, hands it to the composer, and lists omp's values with their descriptions.</summary>
    [Fact]
    public async Task TheChipAppearsWhenOmpReportsTheOptionAndDisappearsWhenItIsGone()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var control = new FakeSessionOptions();
            OptionChipModel? assigned = null;
            var assignments = 0;
            using var binding = new OptionChipBinding(control, "mode", "Mode", chip =>
            {
                assigned = chip;
                assignments++;
            });

            binding.Chip.ShouldBeNull();
            assigned.ShouldBeNull();

            control.Report(FakeSessionOptions.Mode(), FakeSessionOptions.Thinking());

            var chip = binding.Chip.ShouldNotBeNull();
            assigned.ShouldBeSameAs(chip);
            chip.OptionId.ShouldBe("mode");
            chip.Choices.Select(static choice => choice.Value).ShouldBe(["default", "plan"]);
            chip.Choices[1].Description.ShouldBe(FakeSessionOptions.PlanDescription);
            chip.Label.ShouldBe("Default");

            control.Report(FakeSessionOptions.Mode("plan"), FakeSessionOptions.Thinking());
            binding.Chip.ShouldBeSameAs(chip, "a change keeps the chip");
            chip.Label.ShouldBe("Plan");
            chip.ToolTipText.ShouldBe("Mode: Plan. " + FakeSessionOptions.PlanDescription);

            control.Report();
            binding.Chip.ShouldBeNull();
            assigned.ShouldBeNull();
            assignments.ShouldBe(2, "created once, kept on a change, removed once");
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Choosing a value goes to omp under the option's own id; omp's confirmation moves the label.</summary>
    [Fact]
    public async Task ChoosingAValueChangesTheOptionThroughOmp()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var control = new FakeSessionOptions();
            control.Report(FakeSessionOptions.Mode(), FakeSessionOptions.Thinking());
            using var binding = new OptionChipBinding(control, "thinking", "Thinking", static _ => { });
            var thinking = binding.Chip.ShouldNotBeNull();

            await thinking.SelectAsync(thinking.Choices[2]).ConfigureAwait(true);

            control.Requests.ShouldBe([("thinking", "high")]);
            thinking.Label.ShouldBe("high");
            thinking.Notice.ShouldBeNull();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A change omp refuses keeps the value and shows omp's sentence as the notice.</summary>
    [Fact]
    public async Task ARefusedChangeBecomesTheNoticeAndKeepsTheValue()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var control = new FakeSessionOptions
            {
                Answer = static (_, _) => new OmpOptionSetResult(OmpOptionSetOutcome.Busy, "default", "The setting cannot be changed while a response is running."),
            };
            control.Report(FakeSessionOptions.Mode());
            using var binding = new OptionChipBinding(control, "mode", "Mode", static _ => { });
            var chip = binding.Chip.ShouldNotBeNull();

            await chip.SelectAsync(chip.Choices[1]).ConfigureAwait(true);

            chip.Notice.ShouldBe("The setting cannot be changed while a response is running.");
            chip.CurrentValue.ShouldBe("default");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A disposed binding stops following the control.</summary>
    [Fact]
    public async Task ADisposedBindingStopsFollowing()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var control = new FakeSessionOptions();
            var binding = new OptionChipBinding(control, "mode", "Mode", static _ => { });
            binding.Dispose();

            control.Report(FakeSessionOptions.Mode());

            binding.Chip.ShouldBeNull();
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
