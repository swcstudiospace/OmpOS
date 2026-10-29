using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Autonogrammer.Api.IntegrationTests.Harness;

/// <summary>A host environment with a chosen name, for code that only asks whether the host is Development.</summary>
internal sealed class TestHostEnvironment : IHostEnvironment
{
    /// <summary>Initializes a new instance of the <see cref="TestHostEnvironment"/> class.</summary>
    /// <param name="environmentName">The environment name, for example <c>Production</c>.</param>
    public TestHostEnvironment(string environmentName)
    {
        EnvironmentName = environmentName;
    }

    /// <summary>Gets the Development environment.</summary>
    public static TestHostEnvironment Development { get; } = new(Environments.Development);

    /// <summary>Gets the Production environment.</summary>
    public static TestHostEnvironment Production { get; } = new(Environments.Production);

    /// <inheritdoc />
    public string EnvironmentName { get; set; }

    /// <inheritdoc />
    public string ApplicationName { get; set; } = "Autonogrammer.Api.IntegrationTests";

    /// <inheritdoc />
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    /// <inheritdoc />
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
