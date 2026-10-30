using System.Diagnostics.CodeAnalysis;

namespace Autonogrammer.Workflows.Tests;

/// <summary>
/// Fixture interface for providing activities for contract tests.
/// </summary>
[SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Implemented by the public contract test fixtures; the generic parametrization requires consistent accessibility.")]
public interface ISessionRunWorkflowFixture
{
    /// <summary>Gets the test activities instance.</summary>
    TestSessionRunActivities Activities { get; }
}
