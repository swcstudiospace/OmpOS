using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OmpOS.Harness.Abstractions;
using OmpOS.Sandbox.Abstractions;
using OmpOS.Sessions;
using OmpOS.Workflows.Worker;
using Temporalio.Client;
using Xunit;

namespace OmpOS.Workflows.Tests;

/// <summary>
/// Tests for the Temporal Cloud connection options: API key and TLS binding, and API key secrecy.
/// </summary>
public sealed class WorkerConnectionOptionsTests
{
    private const string SecretKey = "ompos-test-api-key-7f3a";

    /// <summary>
    /// Verifies a configured API key is bound from <c>Workflows:ApiKey</c>.
    /// </summary>
    [Fact]
    public void ReadOptions_ApiKeyPresent_IsBound()
    {
        // Arrange
        using var configuration = new ConfigurationManager();
        configuration["Workflows:ApiKey"] = SecretKey;

        // Act
        var options = WorkerHostConfiguration.ReadOptions(configuration);

        // Assert
        Assert.Equal(SecretKey, options.ApiKey);
    }

    /// <summary>
    /// Verifies an absent API key binds to null.
    /// </summary>
    [Fact]
    public void ReadOptions_ApiKeyAbsent_IsNull()
    {
        // Arrange
        using var configuration = new ConfigurationManager();

        // Act
        var options = WorkerHostConfiguration.ReadOptions(configuration);

        // Assert
        Assert.Null(options.ApiKey);
    }

    /// <summary>
    /// Verifies an empty or whitespace-only API key counts as absent.
    /// </summary>
    /// <param name="apiKey">The blank configured value.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ReadOptions_ApiKeyBlank_CountsAsAbsent(string apiKey)
    {
        // Arrange
        using var configuration = new ConfigurationManager();
        configuration["Workflows:ApiKey"] = apiKey;

        // Act
        var options = WorkerHostConfiguration.ReadOptions(configuration);

        // Assert
        Assert.Null(options.ApiKey);
    }

    /// <summary>
    /// Verifies TLS defaults to false when <c>Workflows:Tls</c> is absent.
    /// </summary>
    [Fact]
    public void ReadOptions_TlsAbsent_DefaultsToFalse()
    {
        // Arrange
        using var configuration = new ConfigurationManager();

        // Act
        var options = WorkerHostConfiguration.ReadOptions(configuration);

        // Assert
        Assert.False(options.Tls);
    }

    /// <summary>
    /// Verifies <c>Workflows:Tls=true</c> enables the TLS flag.
    /// </summary>
    /// <param name="tls">The configured boolean text.</param>
    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    public void ReadOptions_TlsTrue_IsBound(string tls)
    {
        // Arrange
        using var configuration = new ConfigurationManager();
        configuration["Workflows:Tls"] = tls;

        // Act
        var options = WorkerHostConfiguration.ReadOptions(configuration);

        // Assert
        Assert.True(options.Tls);
    }

    /// <summary>
    /// Verifies a configured API key and TLS flag reach the Temporal client connect options.
    /// </summary>
    [Fact]
    public void ConfigureServices_ApiKeyAndTls_ClientOptionsCarryBoth()
    {
        // Arrange
        var options = new WorkerOptions { Enabled = true, ApiKey = SecretKey, Tls = true };

        // Act
        var clientOptions = ResolveClientOptions(options);

        // Assert
        Assert.Equal(SecretKey, clientOptions.ApiKey);
        Assert.False(clientOptions.Tls?.Disabled ?? true);
    }

    /// <summary>
    /// Verifies that without an API key or TLS flag the client carries no key and no explicit TLS.
    /// </summary>
    [Fact]
    public void ConfigureServices_ApiKeyAbsentAndTlsFalse_ClientHasNoKeyAndNoTls()
    {
        // Arrange
        var options = new WorkerOptions { Enabled = true, ApiKey = null, Tls = false };

        // Act
        var clientOptions = ResolveClientOptions(options);

        // Assert
        Assert.Null(clientOptions.ApiKey);
        Assert.Null(clientOptions.Tls);
    }

    /// <summary>
    /// Verifies that an API key with TLS left false never explicitly disables TLS, so the SDK keeps it enabled.
    /// </summary>
    [Fact]
    public void ConfigureServices_ApiKeyWithTlsFalse_DoesNotDisableTls()
    {
        // Arrange
        var options = new WorkerOptions { Enabled = true, ApiKey = SecretKey, Tls = false };

        // Act
        var clientOptions = ResolveClientOptions(options);

        // Assert
        Assert.Equal(SecretKey, clientOptions.ApiKey);
        Assert.False(clientOptions.Tls?.Disabled ?? false);
    }

    /// <summary>
    /// Verifies the API key is absent from the exception raised by the disabled registration guard.
    /// </summary>
    [Fact]
    public void ConfigureServices_DisabledWithApiKey_ExceptionOmitsKey()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new WorkerOptions { Enabled = false, ApiKey = SecretKey };

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => WorkerHostConfiguration.ConfigureServices(services, options));

        // Assert
        Assert.False(exception.Message.Contains(SecretKey, StringComparison.Ordinal));
    }

    /// <summary>
    /// Verifies the API key is absent from the exception raised by option validation.
    /// </summary>
    [Fact]
    public void ValidateOptions_EnabledWithoutAddressAndApiKey_ExceptionOmitsKey()
    {
        // Arrange
        var options = new WorkerOptions { Enabled = true, Address = " ", ApiKey = SecretKey };

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => WorkerHostConfiguration.ValidateOptions(options));

        // Assert
        Assert.False(exception.Message.Contains(SecretKey, StringComparison.Ordinal));
    }

    private static TemporalClientConnectOptions ResolveClientOptions(WorkerOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISandboxProvider>(new StubSandboxProvider());
        services.AddSingleton<IHarnessSessionFactory>(new StubHarnessSessionFactory());
        services.AddSingleton<ISessionRepository>(new InMemorySessionRepository());
        WorkerHostConfiguration.ConfigureServices(services, options);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<TemporalClientConnectOptions>>().Value;
    }
}
