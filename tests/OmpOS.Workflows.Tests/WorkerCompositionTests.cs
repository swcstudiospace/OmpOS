using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Harness.Abstractions;
using OmpOS.Sandbox.Abstractions;
using OmpOS.Workflows.Worker;
using Shouldly;
using Xunit;

namespace OmpOS.Workflows.Tests;

/// <summary>
/// Tests the startup guards that require every dependency of the session-run activities, including the session store, to be
/// registered by the host (OD-02, WR-10). The run activities themselves are exercised by <c>OmpOS.SessionRuns.Tests</c>.
/// </summary>
[SuppressMessage("Usage", "CA2007:Do not directly await a Task", Justification = "xUnit test methods await on the test context; ConfigureAwait(true) is the test default.")]
[SuppressMessage("Usage", "MA0004:Use Task.ConfigureAwait", Justification = "Same as CA2007: test methods await on the test context.")]
public sealed class WorkerCompositionTests
{
    /// <summary>Verifies startup fails and names the sandbox provider key when no provider is registered.</summary>
    [Fact]
    public void ConfigureServices_MissingSandboxProvider_FailsAtStartupNamingKey()
    {
        // Arrange — the harness factory is registered; the sandbox provider is not.
        var services = new ServiceCollection();
        services.AddSingleton<IHarnessSessionFactory>(new StubHarnessSessionFactory());

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => WorkerHostConfiguration.ConfigureServices(services, new WorkerOptions { Enabled = true }));
        ex.Message.ShouldContain("Sandbox:Provider");
    }

    /// <summary>Verifies startup fails and names the harness adapter key when no harness factory is registered.</summary>
    [Fact]
    public void ConfigureServices_MissingHarnessFactory_FailsAtStartupNamingKey()
    {
        // Arrange — the sandbox provider is registered; the harness factory is not.
        var services = new ServiceCollection();
        services.AddSingleton<ISandboxProvider>(new StubSandboxProvider());

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => WorkerHostConfiguration.ConfigureServices(services, new WorkerOptions { Enabled = true }));
        ex.Message.ShouldContain("Harness:Adapter");
    }

    /// <summary>Verifies startup fails and names the session store key when the host registers no session store.</summary>
    [Fact]
    public void ConfigureServices_MissingSessionStore_FailsAtStartupNamingKey()
    {
        // Arrange — the sandbox provider and harness factory are registered; the session store is not.
        var services = new ServiceCollection();
        services.AddSingleton<ISandboxProvider>(new StubSandboxProvider());
        services.AddSingleton<IHarnessSessionFactory>(new StubHarnessSessionFactory());

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => WorkerHostConfiguration.ConfigureServices(services, new WorkerOptions { Enabled = true }));
        ex.Message.ShouldContain(WorkerHostConfiguration.SessionStoreKey);
    }

    /// <summary>Verifies configured startup without a session connection string fails and names the key, so the worker never falls back to in-memory storage.</summary>
    [Fact]
    public void ConfigureServices_FromConfigurationWithoutSessionConnectionString_FailsNamingKey()
    {
        // Arrange — every selector is valid; only the session connection string is missing.
        using var configuration = new ConfigurationManager();
        configuration["Workflows:Enabled"] = "true";
        configuration["Sandbox:Provider"] = "podman";
        configuration["Harness:Adapter"] = "rpc";
        configuration["Harness:OmpPath"] = "omp";
        configuration["Harness:Cwd"] = Path.GetTempPath();
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => WorkerHostConfiguration.ConfigureServices(services, configuration));
        ex.Message.ShouldContain("Sessions:Postgres:ConnectionString");
    }
}
