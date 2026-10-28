using Microsoft.Extensions.Options;
using OmpOS.Harness.Automations.Hosted;

namespace OmpOS.Api.Automations;

/// <summary>
/// Runs the cron scheduler while the host runs. At start it marks the runs a previous process left as running (recovery), then ticks the
/// scheduler until the host stops. It does nothing when automations are off or <c>Automations:SchedulerEnabled</c> is false.
/// </summary>
internal sealed class AutomationHostedService : BackgroundService
{
    private readonly IOptions<AutomationOptions> _options;
    private readonly IServiceProvider _services;

    /// <summary>Initializes a new instance of the <see cref="AutomationHostedService"/> class.</summary>
    /// <param name="options">The automation options.</param>
    /// <param name="services">The service provider; the automation services are resolved only when automations are on.</param>
    public AutomationHostedService(IOptions<AutomationOptions> options, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(services);
        _options = options;
        _services = services;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled)
        {
            return;
        }

        var runner = _services.GetRequiredService<AutomationRunner>();
        _ = await runner.RecoverInterruptedRunsAsync(stoppingToken).ConfigureAwait(false);
        if (_options.Value.SchedulerEnabled)
        {
            await _services.GetRequiredService<AutomationScheduler>().RunAsync(stoppingToken).ConfigureAwait(false);
        }
    }
}
