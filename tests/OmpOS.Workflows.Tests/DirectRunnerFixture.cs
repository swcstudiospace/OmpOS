using System.Diagnostics.CodeAnalysis;

namespace OmpOS.Workflows.Tests;

/// <summary>
/// Fixture for DirectSessionRunner contract tests.
/// </summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "xUnit instantiates the fixture for the generic contract tests; the public contract parametrization requires consistent accessibility.")]
public sealed class DirectRunnerFixture : ISessionRunWorkflowFixture
{
    /// <inheritdoc />
    public TestSessionRunActivities Activities { get; } = new();

    /// <summary>Creates a direct-runner workflow over the test activities.</summary>
    /// <returns>The workflow under test.</returns>
    public ISessionRunWorkflow CreateWorkflow() => new DirectSessionRunner(Activities);
}
