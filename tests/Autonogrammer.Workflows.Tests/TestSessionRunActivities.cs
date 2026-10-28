using System.Diagnostics.CodeAnalysis;
using Autonogrammer.Contracts.Sandboxes;

namespace Autonogrammer.Workflows.Tests;

/// <summary>
/// Test activities with configurable behavior and call tracking for contract tests.
/// </summary>
/// <remarks>
/// When a behavior delegate is set it replaces the default; otherwise the in-memory
/// default runs. <see cref="Reset"/> clears all overrides and tracking between tests
/// because the fixture instance is shared across the test class.
/// </remarks>
[SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Exposed by the public contract test fixtures; xUnit creates those fixtures outside the test methods.")]
public sealed class TestSessionRunActivities : ISessionRunActivities
{
    /// <summary>Gets or sets the provision override.</summary>
    public Func<SessionRunInput, CancellationToken, Task<SandboxDescriptor>>? ProvisionSandboxOverride { get; set; }

    /// <summary>Gets or sets the prompt override.</summary>
    public Func<SessionRunInput, Action<string>, CancellationToken, Task<long>>? RunPromptOverride { get; set; }

    /// <summary>Gets or sets the collect override.</summary>
    public Func<SessionRunInput, CancellationToken, Task<IReadOnlyList<string>>>? CollectArtifactsOverride { get; set; }

    /// <summary>Gets or sets the teardown override.</summary>
    public Func<SessionRunInput, CancellationToken, Task>? TeardownSandboxOverride { get; set; }

    /// <summary>Gets whether provision was called.</summary>
    public bool ProvisionSandboxCalled { get; private set; }

    /// <summary>Gets whether run-prompt was called.</summary>
    public bool RunPromptCalled { get; private set; }

    /// <summary>Gets whether collect was called.</summary>
    public bool CollectArtifactsCalled { get; private set; }

    /// <summary>Gets whether teardown was called.</summary>
    public bool TeardownSandboxCalled { get; private set; }

    /// <inheritdoc />
    public Task<SandboxDescriptor> ProvisionSandboxAsync(SessionRunInput input, CancellationToken cancellationToken)
    {
        ProvisionSandboxCalled = true;
        if (ProvisionSandboxOverride != null)
        {
            return ProvisionSandboxOverride(input, cancellationToken);
        }

        return Task.FromResult(CreateSandbox(input));
    }

    /// <inheritdoc />
    public Task<long> RunPromptAsync(SessionRunInput input, Action<string> heartbeat, CancellationToken cancellationToken)
    {
        RunPromptCalled = true;
        if (RunPromptOverride != null)
        {
            return RunPromptOverride(input, heartbeat, cancellationToken);
        }

        heartbeat("seq:1");
        return Task.FromResult(1L);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> CollectArtifactsAsync(SessionRunInput input, CancellationToken cancellationToken)
    {
        CollectArtifactsCalled = true;
        if (CollectArtifactsOverride != null)
        {
            return CollectArtifactsOverride(input, cancellationToken);
        }

        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    /// <inheritdoc />
    public Task TeardownSandboxAsync(SessionRunInput input, CancellationToken cancellationToken)
    {
        TeardownSandboxCalled = true;
        if (TeardownSandboxOverride != null)
        {
            return TeardownSandboxOverride(input, cancellationToken);
        }

        return Task.CompletedTask;
    }

    /// <summary>Clears all behavior overrides and call tracking.</summary>
    public void Reset()
    {
        ProvisionSandboxOverride = null;
        RunPromptOverride = null;
        CollectArtifactsOverride = null;
        TeardownSandboxOverride = null;
        ProvisionSandboxCalled = false;
        RunPromptCalled = false;
        CollectArtifactsCalled = false;
        TeardownSandboxCalled = false;
    }

    /// <summary>Creates the default in-memory sandbox descriptor for an input.</summary>
    /// <param name="input">The workflow input.</param>
    /// <returns>The default descriptor.</returns>
    public static SandboxDescriptor CreateSandbox(SessionRunInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new SandboxDescriptor
        {
            Id = input.SandboxId,
            SessionId = input.SessionId,
            Isolation = Isolation.Container,
            Attestation = Attestation.None,
            NativeBinaries = false,
            TtlSeconds = 1800,
            Labels = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["autonogrammer.session"] = input.SessionId,
                ["autonogrammer.idempotency"] = input.IdempotencyKey,
            },
        };
    }
}
