using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OmpOS.Workflows.Worker;
using Temporalio.Client;
using Temporalio.Extensions.Hosting;
using Xunit;
namespace OmpOS.Workflows.Tests;

/// <summary>
/// Tests for the Temporal worker host configuration behavior.
/// </summary>
/// <remarks>
/// When <c>Workflows:Enabled=false</c> (default) <c>Program.Main</c> returns 0 before any
/// Temporal service is registered; when enabled, <see cref="WorkerHostConfiguration"/>
/// registers the Temporal client and the hosted session-run worker.
/// </remarks>
[SuppressMessage("Usage", "CA2007:Do not directly await a Task", Justification = "xUnit test methods await on the test context; ConfigureAwait(true) is the test default.")]
[SuppressMessage("Usage", "MA0004:Use Task.ConfigureAwait", Justification = "Same as CA2007: test methods await on the test context.")]
public sealed class WorkerHostTests
{
    /// <summary>
    /// Verifies the worker entry point exits immediately (code 0) when disabled.
    /// </summary>
    [Fact]
    public async Task WorkerMain_Disabled_ReturnsZero()
    {
        // Arrange — Main reads the process environment for configuration.
        Environment.SetEnvironmentVariable("Workflows__Enabled", "false");
        try
        {
            // Act — must return without building a host or touching Temporal.
            var exitCode = await Program.Main([]);

            // Assert
            Assert.Equal(0, exitCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Workflows__Enabled", null);
        }
    }

    /// <summary>
    /// Verifies the disabled configuration can never register Temporal services (IN-02).
    /// </summary>
    /// <remarks>
    /// The disabled path exits in <c>Program.Main</c> before registration; this pins the
    /// guarantee structurally by refusing disabled options in <c>ConfigureServices</c>, so
    /// the test fails if the early return is ever bypassed rather than passing by construction.
    /// </remarks>
    [Fact]
    public void WorkerHost_Disabled_ConfigureServicesRefusesRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new WorkerOptions { Enabled = false };

        // Act & Assert — no Temporal service is registered; registration itself is refused.
        var ex = Assert.Throws<InvalidOperationException>(
            () => WorkerHostConfiguration.ConfigureServices(services, options));
        Assert.Contains("Workflows:Enabled=true", ex.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    /// <summary>
    /// Verifies the enabled configuration registers the Temporal client and worker.
    /// </summary>
    [Fact]
    public void WorkerHost_Enabled_RegistersTemporalServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new WorkerOptions
        {
            Enabled = true,
            Address = "localhost:7233",
            Namespace = "default",
            TaskQueue = "ompos-session-runs",
        };

        // Act
        WorkerHostConfiguration.ConfigureServices(services, options);

        // Assert — client, hosted worker, and activities are all registered.
        Assert.Contains(services, d => d.ServiceType == typeof(ITemporalClient));
        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService));
        Assert.Contains(services, d => d.ServiceType == typeof(ISessionRunActivities));

        using var provider = services.BuildServiceProvider();
        Assert.Contains(provider.GetServices<IHostedService>(), s => s is TemporalWorkerService);

        var clientOptions = provider.GetRequiredService<IOptions<TemporalClientConnectOptions>>().Value;
        Assert.Same(SessionRunDataConverter.Instance, clientOptions.DataConverter);
    }

    /// <summary>
    /// Verifies options are read from configuration with documented defaults.
    /// </summary>
    [Fact]
    public void WorkerOptions_ReadFromConfiguration()
    {
        // Arrange
        using var configuration = new ConfigurationManager();
        configuration["Workflows:Enabled"] = "true";
        configuration["Workflows:Address"] = "temporal:7233";

        // Act
        var options = WorkerHostConfiguration.ReadOptions(configuration);

        // Assert
        Assert.True(options.Enabled);
        Assert.True(WorkerHostConfiguration.IsEnabled(configuration));
        Assert.Equal("temporal:7233", options.Address);
        Assert.Equal(WorkerOptions.DefaultNamespace, options.Namespace);
        Assert.Equal(WorkerOptions.DefaultTaskQueue, options.TaskQueue);
    }

    /// <summary>
    /// Verifies defaults apply when the section is absent (worker disabled by default).
    /// </summary>
    [Fact]
    public void WorkerOptions_Defaults_Disabled()
    {
        // Arrange
        using var configuration = new ConfigurationManager();

        // Act
        var options = WorkerHostConfiguration.ReadOptions(configuration);

        // Assert
        Assert.False(options.Enabled);
        Assert.False(WorkerHostConfiguration.IsEnabled(configuration));
        Assert.Equal(WorkerOptions.DefaultAddress, options.Address);
    }

    /// <summary>
    /// Verifies validation fails when enabled without an address.
    /// </summary>
    /// <param name="address">The address value under test.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void WorkerOptions_Validation_FailsWhenEnabledWithoutAddress(string address)
    {
        // Arrange
        var options = new WorkerOptions { Enabled = true, Address = address };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => WorkerHostConfiguration.ValidateOptions(options));
        Assert.Contains("Workflows:Address", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the enabled worker registers a fail-loud guard by default (WR-10).
    /// </summary>
    [Fact]
    public async Task WorkerHost_Enabled_RegistersFailLoudGuardByDefault()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new WorkerOptions { Enabled = true };

        // Act
        WorkerHostConfiguration.ConfigureServices(services, options);

        // Assert — resolution succeeds but every activity throws fail-loud.
        using var provider = services.BuildServiceProvider();
        var activities = provider.GetRequiredService<ISessionRunActivities>();
        Assert.IsType<GuardedSessionRunActivities>(activities);

        var input = TestInputFactory.Create();
        var ct = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<InvalidOperationException>(() => activities.ProvisionSandboxAsync(input, ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => activities.RunPromptAsync(input, _ => { }, ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => activities.CollectArtifactsAsync(input, ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => activities.TeardownSandboxAsync(input, ct));
    }

    /// <summary>
    /// Verifies fake activities are opt-in only via explicit configuration (WR-10).
    /// </summary>
    [Fact]
    public void WorkerHost_EnabledFakeActivities_RegistersFakes()
    {
        // Arrange
        using var configuration = new ConfigurationManager();
        configuration["Workflows:Enabled"] = "true";
        configuration["Workflows:EnableFakeActivities"] = "true";
        var options = WorkerHostConfiguration.ReadOptions(configuration);
        var services = new ServiceCollection();

        // Act
        WorkerHostConfiguration.ConfigureServices(services, options);

        // Assert
        using var provider = services.BuildServiceProvider();
        Assert.IsType<FakeSessionRunActivities>(provider.GetRequiredService<ISessionRunActivities>());
    }
}
