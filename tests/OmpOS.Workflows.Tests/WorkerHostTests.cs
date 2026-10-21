using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Rpc;
using OmpOS.Sandbox;
using OmpOS.Sandbox.Abstractions;
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
/// selects the sandbox provider and harness adapter, then registers the Temporal client and the hosted
/// session-run worker.
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
    /// Verifies the disabled entry point takes its early-return branch, exits with code 0, and never
    /// builds the Temporal host (IN-02).
    /// </summary>
    /// <remarks>
    /// <c>Program.Main</c> builds its host locally, so the registrations it would make cannot be read
    /// from outside. The disabled branch returns before <c>ConfigureServices</c> and <c>Build</c>. If that
    /// branch were bypassed, <c>ConfigureServices</c> would refuse the disabled options and <c>Main</c>
    /// would throw, so this test fails instead of passing by construction.
    /// </remarks>
    [Fact]
    public async Task WorkerHost_Disabled_NoTemporalServicesRegistered()
    {
        // Arrange — Main reads the process environment; capture its console banner to prove the branch ran.
        Environment.SetEnvironmentVariable("Workflows__Enabled", "false");
        using var output = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);
        try
        {
            // Act — must return the disabled exit code without building a host.
            var exitCode = await Program.Main([]);

            // Assert — the disabled branch wrote its banner and exited with code 0.
            Assert.Equal(0, exitCode);
            Assert.Contains("Workflows:Enabled=false", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
            Environment.SetEnvironmentVariable("Workflows__Enabled", null);
        }
    }

    /// <summary>
    /// Verifies the enabled configuration registers the Temporal client and worker.
    /// </summary>
    [Fact]
    public void WorkerHost_Enabled_RegistersTemporalServices()
    {
        // Arrange — the host supplies the sandbox provider and harness factory; the worker registers no stand-ins.
        var services = new ServiceCollection();
        services.AddSingleton<ISandboxProvider>(new StubSandboxProvider());
        services.AddSingleton<IHarnessSessionFactory>(new StubHarnessSessionFactory());
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
    /// Verifies the options overload refuses to register anything when the worker is disabled (the guard).
    /// </summary>
    [Fact]
    public void ConfigureServices_DisabledOptions_ThrowsAndRegistersNothing()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new WorkerOptions { Enabled = false };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => WorkerHostConfiguration.ConfigureServices(services, options));
        Assert.Contains("Workflows:Enabled=true", ex.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    /// <summary>
    /// Verifies the configuration overload refuses a disabled worker before it selects any dependency.
    /// </summary>
    [Fact]
    public void ConfigureServices_FromConfigurationWhenDisabled_ThrowsBeforeSelectingDependencies()
    {
        // Arrange — both selectors are set, so only the guard can stop registration before they run.
        using var configuration = new ConfigurationManager();
        configuration["Sandbox:Provider"] = "podman";
        configuration["Harness:Adapter"] = "rpc";
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => WorkerHostConfiguration.ConfigureServices(services, configuration));
        Assert.Contains("Workflows:Enabled=true", ex.Message, StringComparison.Ordinal);
        Assert.Empty(services);
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
    /// Verifies a missing or unsupported <c>Sandbox:Provider</c> fails startup and names the key. Only <c>podman</c> is composed.
    /// </summary>
    /// <param name="provider">The configured provider value under test.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("openshell")]
    [InlineData("agentos")]
    [InlineData("railway")]
    public void RegisterSandboxProvider_MissingOrUnsupported_FailsNamingKey(string? provider)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => WorkerHostConfiguration.RegisterSandboxProvider(services, provider));
        Assert.Contains("Sandbox:Provider", ex.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    /// <summary>
    /// Verifies the <c>podman</c> provider resolves to the local podman-compose provider.
    /// </summary>
    [Fact]
    public void RegisterSandboxProvider_Podman_ResolvesLocalPodmanComposeProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        WorkerHostConfiguration.RegisterSandboxProvider(services, "podman");

        // Assert — resolution builds the provider through its constructor only; no podman process is started.
        using var provider = services.BuildServiceProvider();
        Assert.IsType<LocalPodmanComposeProvider>(provider.GetRequiredService<ISandboxProvider>());
    }

    /// <summary>
    /// Verifies <c>Harness:Adapter=rpc</c> with an omp path and working directory resolves the RPC-backed harness session factory.
    /// </summary>
    [Fact]
    public void RegisterHarnessAdapter_Rpc_ResolvesRpcHarnessSessionFactory()
    {
        // Arrange
        using var configuration = new ConfigurationManager();
        configuration["Harness:Adapter"] = "rpc";
        configuration["Harness:OmpPath"] = "omp";
        configuration["Harness:Cwd"] = Path.GetTempPath();
        var services = new ServiceCollection();

        // Act
        WorkerHostConfiguration.RegisterHarnessAdapter(services, configuration);

        // Assert — resolving the factory builds the client only; no engine starts until a session is created.
        using var provider = services.BuildServiceProvider();
        Assert.IsType<RpcHarnessSessionFactory>(provider.GetRequiredService<IHarnessSessionFactory>());
    }

    /// <summary>
    /// Verifies every <c>Harness:Adapter</c> value other than <c>rpc</c>, including the in-memory scripted harness,
    /// fails startup and names the key.
    /// </summary>
    /// <param name="adapter">The configured adapter value under test.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("inmemory")]
    [InlineData("InMemory")]
    [InlineData("RPC")]
    [InlineData("rpc ")]
    public void RegisterHarnessAdapter_UnsupportedValue_FailsNamingKey(string? adapter)
    {
        // Arrange
        using var configuration = new ConfigurationManager();
        configuration["Harness:Adapter"] = adapter;
        configuration["Harness:OmpPath"] = "omp";
        configuration["Harness:Cwd"] = Path.GetTempPath();
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => WorkerHostConfiguration.RegisterHarnessAdapter(services, configuration));
        Assert.Contains("Harness:Adapter", ex.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    /// <summary>
    /// Verifies the <c>rpc</c> adapter fails startup and names <c>Harness:OmpPath</c> when the omp path is missing or blank.
    /// </summary>
    /// <param name="ompPath">The configured omp path under test.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterHarnessAdapter_RpcWithoutOmpPath_FailsNamingKey(string? ompPath)
    {
        // Arrange
        using var configuration = new ConfigurationManager();
        configuration["Harness:Adapter"] = "rpc";
        configuration["Harness:OmpPath"] = ompPath;
        configuration["Harness:Cwd"] = Path.GetTempPath();
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => WorkerHostConfiguration.RegisterHarnessAdapter(services, configuration));
        Assert.Contains("Harness:OmpPath", ex.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    /// <summary>
    /// Verifies the <c>rpc</c> adapter fails startup and names <c>Harness:Cwd</c> when the working directory is missing or blank.
    /// </summary>
    /// <param name="cwd">The configured working directory under test.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterHarnessAdapter_RpcWithoutCwd_FailsNamingKey(string? cwd)
    {
        // Arrange
        using var configuration = new ConfigurationManager();
        configuration["Harness:Adapter"] = "rpc";
        configuration["Harness:OmpPath"] = "omp";
        configuration["Harness:Cwd"] = cwd;
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => WorkerHostConfiguration.RegisterHarnessAdapter(services, configuration));
        Assert.Contains("Harness:Cwd", ex.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    /// <summary>
    /// Verifies enabled startup with a valid sandbox provider still fails at harness selection and names the key.
    /// </summary>
    [Fact]
    public void ConfigureServices_EnabledWithoutHarnessAdapter_FailsNamingKey()
    {
        // Arrange
        using var configuration = new ConfigurationManager();
        configuration["Workflows:Enabled"] = "true";
        configuration["Sandbox:Provider"] = "podman";
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => WorkerHostConfiguration.ConfigureServices(services, configuration));
        Assert.Contains("Harness:Adapter", ex.Message, StringComparison.Ordinal);
    }
}
