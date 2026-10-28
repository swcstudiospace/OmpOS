using Autonogrammer.Harness.Automations.Hosted;

namespace Autonogrammer.Native.Tests.Composition.Automations;

/// <summary>Builders of the automations the tests of the desktop wiring run.</summary>
internal static class AutomationFixtures
{
    /// <summary>The prompt the fixtures run.</summary>
    public const string Prompt = "Summarise the open pull requests.";

    /// <summary>Creates a local automation that fires every minute and was created at <paramref name="createdAt"/>.</summary>
    /// <param name="createdAt">When the automation was created: the scheduler counts from there.</param>
    /// <param name="id">The identifier.</param>
    /// <param name="prompt">The prompt.</param>
    /// <returns>The automation.</returns>
    public static UserAutomation Local(DateTimeOffset createdAt, string id = "auto-1", string prompt = Prompt) => new()
    {
        Id = id,
        Owner = "local",
        Name = "Morning summary",
        Prompt = prompt,
        Enabled = true,
        Triggers = [new AutomationTriggerSpec { Kind = AutomationTriggerKind.Cron, Cron = "* * * * *" }],
        Target = new AutomationTargetSpec { Kind = AutomationTargetKind.Local },
        CreatedAt = createdAt,
        UpdatedAt = createdAt,
    };

    /// <summary>Creates the run request the runner hands a target.</summary>
    /// <param name="automation">The automation.</param>
    /// <returns>The request.</returns>
    public static AutomationRunRequest Request(UserAutomation automation) =>
        new(automation, "run-1", automation.Prompt, AutomationTriggerContext.Manual());

    /// <summary>Waits, without blocking, until an asynchronous condition holds; fails the test after thirty seconds.</summary>
    /// <param name="condition">The condition, read again every few milliseconds.</param>
    /// <param name="description">What the test waits for.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the condition holds.</returns>
    /// <exception cref="TimeoutException">The condition did not hold in time.</exception>
    public static async Task EventuallyAsync(Func<Task<bool>> condition, string description, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!await condition().ConfigureAwait(true))
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Timed out waiting for: " + description + ".");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(15), cancellationToken).ConfigureAwait(true);
        }
    }
}
