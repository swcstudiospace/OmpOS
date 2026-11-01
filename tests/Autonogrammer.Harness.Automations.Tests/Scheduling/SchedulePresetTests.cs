using Autonogrammer.Harness.Automations.Cron;
using Autonogrammer.Harness.Automations.Scheduling;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Automations.Tests.Scheduling;

/// <summary>Verifies each schedule preset maps to a five-field cron expression. Weekdays is Monday to Friday.</summary>
public sealed class SchedulePresetTests
{
    [Theory]
    [InlineData(SchedulePreset.Hourly, "0 * * * *")]
    [InlineData(SchedulePreset.Daily, "0 9 * * *")]
    [InlineData(SchedulePreset.Weekly, "0 9 * * 1")]
    [InlineData(SchedulePreset.Weekdays, "0 9 * * 1-5")]
    [InlineData(SchedulePreset.Manual, "0 0 31 2 *")]
    public void Each_preset_maps_to_a_five_field_cron_expression(SchedulePreset preset, string cron)
    {
        SchedulePresetMap.ToCron(preset).ShouldBe(cron);
        cron.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length.ShouldBe(5);
        CronExpression.TryParse(cron, out _, out _).ShouldBeTrue();
    }

    [Fact]
    public void Weekdays_is_Monday_to_Friday()
    {
        var cron = SchedulePresetMap.ToCron(SchedulePreset.Weekdays);
        cron.ShouldEndWith("1-5");
    }
}
