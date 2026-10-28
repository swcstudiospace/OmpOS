using OmpOS.Api.Services.Providers;

namespace OmpOS.Api.IntegrationTests.Providers;

/// <summary>An environment that holds exactly the variables a test sets.</summary>
internal sealed class FakeProviderEnvironment : IProviderEnvironment
{
    private readonly HashSet<string> _set;

    /// <summary>Initializes a new instance of the <see cref="FakeProviderEnvironment"/> class.</summary>
    /// <param name="names">The variables that are set.</param>
    public FakeProviderEnvironment(params string[] names) => _set = new HashSet<string>(names, StringComparer.Ordinal);

    /// <inheritdoc />
    public bool IsSet(string name) => _set.Contains(name);
}
