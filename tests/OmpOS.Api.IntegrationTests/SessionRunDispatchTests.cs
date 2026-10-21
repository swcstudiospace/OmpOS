using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Api.Options;
using OmpOS.Api.Workflows;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Workflows;
using OmpOS.Harness.Abstractions;
using OmpOS.Workflows;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// Verifies the session-run dispatcher selected by <c>Workflows:Enabled</c>: the default in-process path is
/// unchanged, the Temporal path starts the workflow on the configured task queue with the idempotency key in its
/// id, and an enabled configuration without <c>Workflows:Address</c> fails startup naming that key.
/// </summary>
public sealed class SessionRunDispatchTests
{
    private const string TaskQueue = "ompos-test-queue";
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Verifies the default configuration selects the in-process dispatcher.</summary>
    [Fact]
    public void WorkflowsDisabled_SelectsDirectDispatcher()
    {
        using var factory = new OmpApiFactory(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Workflows:Enabled"] = "false" });

        var dispatcher = factory.Services.GetRequiredService<ISessionRunDispatcher>();

        dispatcher.ShouldBeOfType<DirectSessionRunDispatcher>();
    }

    /// <summary>Verifies a prompt still completes through the scripted harness on the default path.</summary>
    [Fact]
    public async Task WorkflowsDisabled_PromptCompletesThroughScriptedHarness()
    {
        var config = new Dictionary<string, string?>(StringComparer.Ordinal) { ["Workflows:Enabled"] = "false" };
        using var factory = new OmpApiFactory(
            config,
            configureServices: services => services.AddSingleton<IHarnessSessionFactory>(new ScriptedHarnessFactory(ScriptedHarnessFactory.DeltaScript(2))));
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"hello\"}", testToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, testToken);

        var turn = await SessionClient.ReadStreamAsync(client, $"/sessions/{id}/events", 50, testToken);
        turn[^1].EventType.ShouldBe("completed", "the turn ends on its completed event");
    }

    /// <summary>Verifies the in-process dispatcher runs the workflow with the input and returns its completion on the handle.</summary>
    [Fact]
    public async Task WorkflowsDisabled_DirectDispatcherRunsWorkflowInProcess()
    {
        var workflow = new RecordingWorkflow();
        using var factory = new OmpApiFactory(
            new Dictionary<string, string?>(StringComparer.Ordinal) { ["Workflows:Enabled"] = "false" },
            configureServices: services => services.AddSingleton<ISessionRunWorkflow>(workflow));
        var dispatcher = factory.Services.GetRequiredService<ISessionRunDispatcher>();
        var input = RunInput("session-1", "idem-1");

        var handle = await dispatcher.StartAsync(input, TestContext.Current.CancellationToken);

        handle.WorkflowId.ShouldBe("session-run-session-1-idem-1");
        handle.RunId.ShouldBe(input.RunId);
        var completion = handle.Completion.ShouldNotBeNull();
        var result = await completion.ConfigureAwait(true);
        result.Status.ShouldBe(WorkflowRunStatus.Completed);
        workflow.Inputs.ShouldHaveSingleItem().ShouldBeSameAs(input);
    }

    /// <summary>Verifies enabled workflows select the Temporal dispatcher, which starts the workflow on the configured task queue with the idempotency key in its id.</summary>
    [Fact]
    public async Task WorkflowsEnabled_StartsWorkflowOnConfiguredTaskQueueWithIdempotencyKeyInId()
    {
        var starter = new RecordingTemporalStarter();
        using var factory = new OmpApiFactory(
            WithDatabase(EnabledConfig(TaskQueue)),
            configureServices: services => services.AddSingleton<ITemporalWorkflowStarter>(starter));
        var dispatcher = factory.Services.GetRequiredService<ISessionRunDispatcher>();
        dispatcher.ShouldBeOfType<TemporalSessionRunDispatcher>();
        var input = RunInput("session-7", "idem-abc");

        var handle = await dispatcher.StartAsync(input, TestContext.Current.CancellationToken);

        var call = starter.Calls.ShouldHaveSingleItem();
        call.WorkflowId.ShouldBe("session-run-session-7-idem-abc");
        call.TaskQueue.ShouldBe(TaskQueue);
        call.Input.ShouldBeSameAs(input);
        handle.WorkflowId.ShouldBe(call.WorkflowId);
        handle.RunId.ShouldBe("run-fake");
        handle.Completion.ShouldBeNull("the worker executes the run; the API holds no in-process completion");
    }

    /// <summary>Verifies the Temporal client settings are mapped from the workflows options.</summary>
    [Fact]
    public void ConnectOptions_CarryAddressNamespaceApiKeyAndTls()
    {
        var options = new WorkflowsOptions
        {
            Enabled = true,
            Address = "temporal.example:7233",
            Namespace = "ompos-prod",
            ApiKey = "  secret-key  ",
            Tls = true,
        };

        var connect = TemporalWorkflowStarter.BuildConnectOptions(options);

        connect.TargetHost.ShouldBe("temporal.example:7233");
        connect.Namespace.ShouldBe("ompos-prod");
        connect.ApiKey.ShouldBe("secret-key");
        connect.Tls.ShouldNotBeNull();
    }

    /// <summary>Verifies a blank API key and disabled TLS leave those client settings unset.</summary>
    [Fact]
    public void ConnectOptions_BlankApiKeyAndTlsOffAreOmitted()
    {
        var options = new WorkflowsOptions { Enabled = true, Address = "localhost:7233", ApiKey = "   " };

        var connect = TemporalWorkflowStarter.BuildConnectOptions(options);

        connect.ApiKey.ShouldBeNull();
        connect.Tls.ShouldBeNull();
    }

    /// <summary>Verifies enabled workflows without a Workflows:Address fail startup naming the key.</summary>
    [Fact]
    public void WorkflowsEnabledWithoutAddress_FailsStartupNamingKey()
    {
        using var factory = new OmpApiFactory(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Workflows:Enabled"] = "true" });

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        Flatten(caught).ShouldContain("Workflows:Address");
    }

    /// <summary>Verifies enabled workflows without Sessions:Postgres:ConnectionString fail startup naming that key.</summary>
    [Fact]
    public void WorkflowsEnabledWithoutConnectionString_FailsStartupNamingKey()
    {
        using var factory = new OmpApiFactory(EnabledConfig(TaskQueue));

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        Flatten(caught).ShouldContain(PostgresTestDatabase.ConnectionStringKey);
    }

    /// <summary>Verifies a blank Workflows:Address fails startup naming the key.</summary>
    /// <param name="address">The blank address value.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void WorkflowsEnabledWithBlankAddress_FailsStartupNamingKey(string address)
    {
        var config = EnabledConfig(TaskQueue);
        config["Workflows:Address"] = address;
        using var factory = new OmpApiFactory(config);

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        Flatten(caught).ShouldContain("Workflows:Address");
    }

    /// <summary>Builds configuration that enables workflows with a fixed address and the given task queue.</summary>
    /// <param name="taskQueue">The configured task queue.</param>
    /// <returns>The configuration overrides.</returns>
    private static Dictionary<string, string?> EnabledConfig(string taskQueue)
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Workflows:Enabled"] = "true",
            ["Workflows:Address"] = "temporal.test:7233",
            ["Workflows:TaskQueue"] = taskQueue,
        };
    }

    /// <summary>Adds the test PostgreSQL connection string to a configuration that enables workflows.</summary>
    /// <param name="config">The configuration overrides.</param>
    /// <returns>The same overrides with the connection string set.</returns>
    private static Dictionary<string, string?> WithDatabase(Dictionary<string, string?> config)
    {
        config[PostgresTestDatabase.ConnectionStringKey] = PostgresTestDatabase.ConnectionString();
        return config;
    }

    /// <summary>Builds a run input for the given session and idempotency key.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="idempotencyKey">The idempotency key.</param>
    /// <returns>The run input.</returns>
    private static SessionRunInput RunInput(string sessionId, string idempotencyKey)
    {
        return new SessionRunInput
        {
            RunId = "run-" + idempotencyKey,
            SessionId = sessionId,
            SandboxId = "sb-" + idempotencyKey,
            CorrelationId = "corr-" + idempotencyKey,
            Prompt = "hello",
            ResumeFromSeq = 0,
            IdempotencyKey = idempotencyKey,
        };
    }

    private static string Flatten(Exception exception)
    {
        var parts = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            parts.Add(current.Message);
        }

        return string.Join(" | ", parts);
    }

    /// <summary>Records every Temporal start and reports a fixed run identifier.</summary>
    private sealed class RecordingTemporalStarter : ITemporalWorkflowStarter
    {
        /// <summary>Gets the recorded start calls in order.</summary>
        public List<StartCall> Calls { get; } = [];

        /// <inheritdoc />
        public Task<string?> StartAsync(string workflowId, string taskQueue, SessionRunInput input, CancellationToken cancellationToken)
        {
            Calls.Add(new StartCall(workflowId, taskQueue, input));
            return Task.FromResult<string?>("run-fake");
        }
    }

    /// <summary>One recorded Temporal start.</summary>
    /// <param name="WorkflowId">The workflow identifier used to start.</param>
    /// <param name="TaskQueue">The task queue used to start.</param>
    /// <param name="Input">The workflow argument.</param>
    private sealed record StartCall(string WorkflowId, string TaskQueue, SessionRunInput Input);

    /// <summary>Records every in-process run and completes each one as Completed.</summary>
    private sealed class RecordingWorkflow : ISessionRunWorkflow
    {
        /// <summary>Gets the recorded run inputs in order.</summary>
        public List<SessionRunInput> Inputs { get; } = [];

        /// <inheritdoc />
        public Task<SessionRunResult> RunAsync(SessionRunInput input, IWorkflowContext ctx, CancellationToken cancellationToken)
        {
            Inputs.Add(input);
            return Task.FromResult(new SessionRunResult { Status = WorkflowRunStatus.Completed, FinalSeq = 0, Artifacts = [] });
        }
    }
}
