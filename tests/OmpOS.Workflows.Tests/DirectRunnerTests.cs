namespace OmpOS.Workflows.Tests;

/// <summary>
/// Contract tests for <see cref="DirectSessionRunner"/>.
/// </summary>
public sealed class DirectRunnerTests : SessionRunWorkflowContractTests<DirectRunnerFixture>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DirectRunnerTests"/> class.
    /// </summary>
    /// <param name="fixture">The test fixture.</param>
    public DirectRunnerTests(DirectRunnerFixture fixture)
        : base(fixture)
    {
    }

    /// <inheritdoc />
    protected override ISessionRunWorkflow CreateWorkflow(DirectRunnerFixture fixture) => fixture.CreateWorkflow();

    /// <inheritdoc />
    protected override IWorkflowContext CreateContext() => new TestWorkflowContext(randomSeed: 42);
}
