using System.Net;
using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Tests for the container and volume lifecycle of <see cref="RailwayCloudEnvironmentClient"/> against recorded Railway fixtures.
/// </summary>
public sealed class RailwayCloudEnvironmentClientTests
{
    private const string WorkerUri = "http://ompos-agent-s1.railway.internal:8080/";

    [Fact]
    public async Task CreateContainerAppliesServiceVariablesSettingsAndOneDeployInOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.RegisterCreateFlow();

        var container = await harness.Client.CreateContainerAsync("s1", ct).ConfigureAwait(true);

        container.ContainerId.ShouldBe("svc-1");
        container.SessionId.ShouldBe("s1");
        container.Phase.ShouldBe(CloudContainerPhase.Running);
        container.BootGeneration.ShouldBe(1);
        container.WorkerBaseUri.ShouldBe(new Uri(WorkerUri));
        string[] expectedOrder =
        [
            "projectServices",
            "serviceCreate",
            "variableCollectionUpsert",
            "serviceInstanceUpdate",
            "serviceInstanceDeployV2",
        ];
        harness.Api.Requests.Select(request => request.Operation).ToArray().ShouldBe(expectedOrder);
        harness.Api.Requests.ShouldAllBe(request => request.Authorization == "Bearer " + RailwayHarness.Token);
        harness.Api.Requests.ShouldAllBe(request => request.Uri == new Uri("https://backboard.railway.com/graphql/v2"));

        var create = harness.Api.Requests[1].Variables["input"]!;
        create["projectId"]!.GetValue<string>().ShouldBe(RailwayHarness.ProjectId);
        create["name"]!.GetValue<string>().ShouldBe(RailwayHarness.Label);
        create["source"]!["image"]!.GetValue<string>().ShouldBe("ghcr.io/swcstudiospace/ompos-agent-worker:test");

        var upsert = harness.Api.Requests[2].Variables["input"]!;
        upsert["projectId"]!.GetValue<string>().ShouldBe(RailwayHarness.ProjectId);
        upsert["environmentId"]!.GetValue<string>().ShouldBe(RailwayHarness.EnvironmentId);
        upsert["serviceId"]!.GetValue<string>().ShouldBe("svc-1");
        upsert["skipDeploys"]!.GetValue<bool>().ShouldBeTrue();
        upsert["replace"]!.GetValue<bool>().ShouldBeFalse();
        upsert["variables"]!["OMPOS_MODE"]!.GetValue<string>().ShouldBe("worker");
        upsert["variables"]!["WORKER_PUBLIC_KEY"]!.GetValue<string>().ShouldBe(RailwayHarness.SecretValue);

        var settings = harness.Api.Requests[3].Variables;
        settings["serviceId"]!.GetValue<string>().ShouldBe("svc-1");
        settings["environmentId"]!.GetValue<string>().ShouldBe(RailwayHarness.EnvironmentId);
        settings["input"]!["sleepApplication"]!.GetValue<bool>().ShouldBeTrue();
        settings["input"]!["healthcheckPath"]!.GetValue<string>().ShouldBe("/healthz");
        settings["input"]!["restartPolicyType"]!.GetValue<string>().ShouldBe("ON_FAILURE");
        settings["input"]!["numReplicas"]!.GetValue<int>().ShouldBe(1);

        var deploy = harness.Api.Requests[4].Variables;
        deploy["serviceId"]!.GetValue<string>().ShouldBe("svc-1");
        deploy["environmentId"]!.GetValue<string>().ShouldBe(RailwayHarness.EnvironmentId);
    }

    [Fact]
    public async Task ServerlessAndHealthcheckFollowTheOptions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness(options =>
        {
            options.EnableServerless = false;
            options.HealthcheckPath = "/ready";
        });
        harness.RegisterCreateFlow();

        await harness.Client.CreateContainerAsync("s1", ct).ConfigureAwait(true);

        var input = harness.Api.Requests.Single(request => string.Equals(request.Operation, "serviceInstanceUpdate", StringComparison.Ordinal)).Variables["input"]!;
        input["sleepApplication"]!.GetValue<bool>().ShouldBeFalse();
        input["healthcheckPath"]!.GetValue<string>().ShouldBe("/ready");
    }

    [Fact]
    public async Task SecondCreateForTheSameSessionIssuesNoServiceCreate()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.RegisterCreateFlow();
        harness.Api.Data("projectServices", RailwayHarness.Services(("svc-1", RailwayHarness.Label)));
        harness.Api.Data("deployments", RailwayHarness.Deployments("SUCCESS"));

        var first = await harness.Client.CreateContainerAsync("s1", ct).ConfigureAwait(true);
        var second = await harness.Client.CreateContainerAsync("s1", ct).ConfigureAwait(true);

        second.ContainerId.ShouldBe(first.ContainerId);
        harness.Api.CountOf("serviceCreate").ShouldBe(1);
        harness.Api.CountOf("variableCollectionUpsert").ShouldBe(1);
        harness.Api.CountOf("serviceInstanceDeployV2").ShouldBe(1);
    }

    [Fact]
    public async Task PreExistingServiceWithTheLabelIsAdoptedWithoutAnyMutation()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("projectServices", RailwayHarness.Services(("svc-other", "unrelated"), ("svc-9", RailwayHarness.Label)));
        harness.Api.Data("deployments", RailwayHarness.Deployments("SUCCESS"));

        var container = await harness.Client.CreateContainerAsync("s1", ct).ConfigureAwait(true);

        container.ContainerId.ShouldBe("svc-9");
        container.Phase.ShouldBe(CloudContainerPhase.Running);
        container.BootGeneration.ShouldBe(1);
        container.WorkerBaseUri.ShouldBe(new Uri(WorkerUri));
        harness.Api.Requests.Select(request => request.Operation).ShouldAllBe(op => op == "projectServices" || op == "deployments");
    }

    [Fact]
    public async Task HalfBuiltServiceIsAdoptedAndItsSettingsReappliedByTheNextCreate()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Add("projectServices", () => RailwayFixtureHandler.Json(HttpStatusCode.OK, "{\"data\":" + RailwayHarness.Services() + "}"));
        harness.Api.Add("projectServices", () => RailwayFixtureHandler.Json(HttpStatusCode.OK, "{\"data\":" + RailwayHarness.Services(("svc-1", RailwayHarness.Label)) + "}"));
        harness.Api.Data("serviceCreate", "{\"serviceCreate\":{\"id\":\"svc-1\",\"name\":\"" + RailwayHarness.Label + "\"}}");
        harness.Api.Data("variableCollectionUpsert", "{\"variableCollectionUpsert\":true}");
        harness.Api.Add("serviceInstanceUpdate", () => RailwayFixtureHandler.Json(
            HttpStatusCode.OK,
            "{\"errors\":[{\"message\":\"settings rejected\",\"extensions\":{\"code\":\"BAD_USER_INPUT\"}}]}"));
        harness.Api.Data("serviceInstanceUpdate", "{\"serviceInstanceUpdate\":true}");
        harness.Api.Data("serviceInstanceDeployV2", "{\"serviceInstanceDeployV2\":\"dep-2\"}");
        harness.Api.Data("deployments", RailwayHarness.Deployments("BUILDING"));

        var failure = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await harness.Client.CreateContainerAsync("s1", ct).ConfigureAwait(true)).ConfigureAwait(true);
        var retried = await harness.Client.CreateContainerAsync("s1", ct).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.GraphQlError);
        failure.Operation.ShouldBe("serviceInstanceUpdate");
        retried.ContainerId.ShouldBe("svc-1");
        harness.Api.CountOf("serviceCreate").ShouldBe(1);
        harness.Api.CountOf("variableCollectionUpsert").ShouldBe(2);
        harness.Api.CountOf("serviceInstanceUpdate").ShouldBe(2);
        harness.Api.CountOf("serviceInstanceDeployV2").ShouldBe(1);
    }

    [Fact]
    public async Task ReconcileReappliesSettingsToAnExistingServiceAndRefusesAMissingOne()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.RegisterCreateFlow();
        harness.Api.Data("projectServices", RailwayHarness.Services(("svc-1", RailwayHarness.Label)));
        harness.Api.Data("deployments", RailwayHarness.Deployments("SUCCESS"));

        var missing = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await harness.Client.ReconcileContainerAsync("s1", ct).ConfigureAwait(true)).ConfigureAwait(true);
        var reconciled = await harness.Client.ReconcileContainerAsync("s1", ct).ConfigureAwait(true);

        missing.Failure.ShouldBe(CloudEnvironmentFailure.NotFound);
        reconciled.ContainerId.ShouldBe("svc-1");
        harness.Api.CountOf("serviceCreate").ShouldBe(0);
        harness.Api.CountOf("variableCollectionUpsert").ShouldBe(1);
        harness.Api.CountOf("serviceInstanceDeployV2").ShouldBe(1);
    }

    [Fact]
    public async Task FindContainerMatchesTheExactLabelOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("projectServices", RailwayHarness.Services(("svc-10", "ompos-agent-s10"), ("svc-x", "ompos-agent-s1-extra")));

        var found = await harness.Client.FindContainerAsync("s1", ct).ConfigureAwait(true);

        found.ShouldBeNull();
        harness.Api.CountOf("deployments").ShouldBe(0);
    }

    [Theory]
    [InlineData("INITIALIZING")]
    [InlineData("BUILDING")]
    [InlineData("DEPLOYING")]
    [InlineData("QUEUED")]
    [InlineData("WAITING")]
    [InlineData("NEEDS_APPROVAL")]
    [InlineData("SUCCESS")]
    public async Task InProgressAndSuccessfulDeploymentsMapToRunning(string status)
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("projectServices", RailwayHarness.Services(("svc-1", RailwayHarness.Label)));
        harness.Api.Data("deployments", RailwayHarness.Deployments(status));

        var container = await harness.Client.FindContainerAsync("s1", ct).ConfigureAwait(true);

        container!.Phase.ShouldBe(CloudContainerPhase.Running);
    }

    [Fact]
    public async Task ServiceWithoutADeploymentYetMapsToRunning()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("projectServices", RailwayHarness.Services(("svc-1", RailwayHarness.Label)));
        harness.Api.Data("deployments", RailwayHarness.Deployments(null));

        var container = await harness.Client.FindContainerAsync("s1", ct).ConfigureAwait(true);

        container!.Phase.ShouldBe(CloudContainerPhase.Running);
    }

    [Fact]
    public async Task SleepingDeploymentMapsToSleeping()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("projectServices", RailwayHarness.Services(("svc-1", RailwayHarness.Label)));
        harness.Api.Data("deployments", RailwayHarness.Deployments("SLEEPING"));

        var container = await harness.Client.FindContainerAsync("s1", ct).ConfigureAwait(true);

        container!.Phase.ShouldBe(CloudContainerPhase.Sleeping);
        container.BootGeneration.ShouldBe(1);
    }

    [Theory]
    [InlineData("REMOVED", CloudEnvironmentFailure.NotFound)]
    [InlineData("REMOVING", CloudEnvironmentFailure.NotFound)]
    [InlineData("FAILED", CloudEnvironmentFailure.InvalidResponse)]
    [InlineData("CRASHED", CloudEnvironmentFailure.InvalidResponse)]
    [InlineData("SKIPPED", CloudEnvironmentFailure.InvalidResponse)]
    [InlineData("NOT_A_STATUS", CloudEnvironmentFailure.InvalidResponse)]
    public async Task BrokenOrRemovedServicesAreNeverAdopted(string status, CloudEnvironmentFailure expected)
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("projectServices", RailwayHarness.Services(("svc-1", RailwayHarness.Label)));
        harness.Api.Data("deployments", RailwayHarness.Deployments(status));

        var failure = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await harness.Client.FindContainerAsync("s1", ct).ConfigureAwait(true)).ConfigureAwait(true);
        var create = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await harness.Client.CreateContainerAsync("s1", ct).ConfigureAwait(true)).ConfigureAwait(true);

        failure.Failure.ShouldBe(expected);
        create.Failure.ShouldBe(expected);
        harness.Api.CountOf("serviceCreate").ShouldBe(0);
        if (expected == CloudEnvironmentFailure.InvalidResponse && !string.Equals(status, "NOT_A_STATUS", StringComparison.Ordinal))
        {
            failure.Message.ShouldContain(status, Case.Insensitive);
        }
    }

    [Fact]
    public async Task GetDeploymentStatusParsesDocumentedStatusesAndNeverThrowsOnUnknownOnes()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("deployments", RailwayHarness.Deployments("SLEEPING"));
        harness.Api.Data("deployments", RailwayHarness.Deployments("CRASHED"));
        harness.Api.Data("deployments", RailwayHarness.Deployments("NEEDS_APPROVAL"));
        harness.Api.Data("deployments", RailwayHarness.Deployments("SOMETHING_NEW"));
        harness.Api.Data("deployments", RailwayHarness.Deployments(null));

        var sleeping = await harness.Client.GetDeploymentStatusAsync("svc-1", ct).ConfigureAwait(true);
        var crashed = await harness.Client.GetDeploymentStatusAsync("svc-1", ct).ConfigureAwait(true);
        var approval = await harness.Client.GetDeploymentStatusAsync("svc-1", ct).ConfigureAwait(true);
        var novel = await harness.Client.GetDeploymentStatusAsync("svc-1", ct).ConfigureAwait(true);
        var none = await harness.Client.GetDeploymentStatusAsync("svc-1", ct).ConfigureAwait(true);

        sleeping.ShouldBe(RailwayDeploymentStatus.Sleeping);
        crashed.ShouldBe(RailwayDeploymentStatus.Crashed);
        approval.ShouldBe(RailwayDeploymentStatus.NeedsApproval);
        novel.ShouldBe(RailwayDeploymentStatus.Unknown);
        none.ShouldBe(RailwayDeploymentStatus.Unknown);
        var variables = harness.Api.Requests[0].Variables;
        variables["first"]!.GetValue<int>().ShouldBe(1);
        variables["input"]!["serviceId"]!.GetValue<string>().ShouldBe("svc-1");
        variables["input"]!["projectId"]!.GetValue<string>().ShouldBe(RailwayHarness.ProjectId);
        variables["input"]!["environmentId"]!.GetValue<string>().ShouldBe(RailwayHarness.EnvironmentId);
    }

    [Fact]
    public async Task CreateVolumeCreatesItAttachedThenNamesItAfterTheSessionLabel()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("projectVolumes", RailwayHarness.Volumes());
        harness.Api.Data("projectServices", RailwayHarness.Services(("svc-1", RailwayHarness.Label)));
        harness.Api.Data("volumeCreate", "{\"volumeCreate\":{\"id\":\"vol-9\",\"name\":\"generated-name\"}}");
        harness.Api.Data("volumeUpdate", "{\"volumeUpdate\":{\"id\":\"vol-9\",\"name\":\"" + RailwayHarness.Label + "\"}}");

        var volume = await harness.Client.CreateVolumeAsync("s1", ct).ConfigureAwait(true);

        volume.ShouldBe(new CloudVolume("vol-9", "s1"));
        string[] expectedOrder = ["projectVolumes", "projectServices", "volumeCreate", "volumeUpdate"];
        harness.Api.Requests.Select(request => request.Operation).ToArray().ShouldBe(expectedOrder);
        var create = harness.Api.Requests[2].Variables["input"]!;
        create["projectId"]!.GetValue<string>().ShouldBe(RailwayHarness.ProjectId);
        create["serviceId"]!.GetValue<string>().ShouldBe("svc-1");
        create["mountPath"]!.GetValue<string>().ShouldBe("/data");
        create["environmentId"]!.GetValue<string>().ShouldBe(RailwayHarness.EnvironmentId);
        var rename = harness.Api.Requests[3].Variables;
        rename["volumeId"]!.GetValue<string>().ShouldBe("vol-9");
        rename["input"]!["name"]!.GetValue<string>().ShouldBe(RailwayHarness.Label);
    }

    [Fact]
    public async Task CreateVolumeIsIdempotentByLabelAndFindVolumeMatchesTheLabel()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("projectVolumes", RailwayHarness.Volumes(("vol-other", "something-else"), ("vol-3", RailwayHarness.Label)));

        var found = await harness.Client.FindVolumeAsync("s1", ct).ConfigureAwait(true);
        var created = await harness.Client.CreateVolumeAsync("s1", ct).ConfigureAwait(true);
        var none = await harness.Client.FindVolumeAsync("other", ct).ConfigureAwait(true);

        found.ShouldBe(new CloudVolume("vol-3", "s1"));
        created.ShouldBe(found);
        none.ShouldBeNull();
        harness.Api.CountOf("volumeCreate").ShouldBe(0);
    }

    [Fact]
    public async Task CreateVolumeWithoutAServiceIsNotFoundAndCreatesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("projectVolumes", RailwayHarness.Volumes());
        harness.Api.Data("projectServices", RailwayHarness.Services(("svc-other", "unrelated")));

        var failure = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await harness.Client.CreateVolumeAsync("s1", ct).ConfigureAwait(true)).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.NotFound);
        harness.Api.CountOf("volumeCreate").ShouldBe(0);
    }

    [Fact]
    public async Task AttachVolumeSendsNothingForTheCreationMountPathAndUpdatesAChangedOne()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("projectServices", RailwayHarness.Services(("svc-1", RailwayHarness.Label)));
        harness.Api.Data("volumeInstanceUpdate", "{\"volumeInstanceUpdate\":true}");

        await harness.Client.AttachVolumeAsync("svc-1", "vol-1", "/data", ct).ConfigureAwait(true);
        harness.Api.CountOf("volumeInstanceUpdate").ShouldBe(0);

        await harness.Client.AttachVolumeAsync("svc-1", "vol-1", "/var/omp/session", ct).ConfigureAwait(true);
        await harness.Client.AttachVolumeAsync("svc-1", "vol-1", "/var/omp/session", ct).ConfigureAwait(true);

        harness.Api.CountOf("volumeInstanceUpdate").ShouldBe(1);
        var update = harness.Api.Requests.Single(request => string.Equals(request.Operation, "volumeInstanceUpdate", StringComparison.Ordinal)).Variables;
        update["volumeId"]!.GetValue<string>().ShouldBe("vol-1");
        update["input"]!["mountPath"]!.GetValue<string>().ShouldBe("/var/omp/session");
    }

    [Fact]
    public async Task AttachVolumeToAnUnknownServiceIsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("projectServices", RailwayHarness.Services());

        var failure = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await harness.Client.AttachVolumeAsync("svc-gone", "vol-1", "/data", ct).ConfigureAwait(true)).ConfigureAwait(true);

        failure.Failure.ShouldBe(CloudEnvironmentFailure.NotFound);
    }

    [Fact]
    public async Task DeleteServiceUsesTheVerifiedMutationAndReportsAFalseResult()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("serviceDelete", "{\"serviceDelete\":true}");
        harness.Api.Data("serviceDelete", "{\"serviceDelete\":false}");

        await harness.Client.DeleteServiceAsync("svc-1", ct).ConfigureAwait(true);
        var failure = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await harness.Client.DeleteServiceAsync("svc-1", ct).ConfigureAwait(true)).ConfigureAwait(true);

        harness.Api.Requests[0].Query.ShouldContain("serviceDelete(id: $id)", Case.Sensitive);
        harness.Api.Requests[0].Variables["id"]!.GetValue<string>().ShouldBe("svc-1");
        failure.Failure.ShouldBe(CloudEnvironmentFailure.GraphQlError);
        failure.Operation.ShouldBe("serviceDelete");
    }

    [Fact]
    public async Task DeleteVolumeSendsVolumeDeleteWithTheVolumeId()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("volumeDelete", "{\"volumeDelete\":true}");

        await harness.Client.DeleteVolumeAsync("vol-7", ct).ConfigureAwait(true);

        harness.Api.Requests.Count.ShouldBe(1);
        harness.Api.Requests[0].Query.ShouldContain("volumeDelete(volumeId: $volumeId)", Case.Sensitive);
        harness.Api.Requests[0].Variables["volumeId"]!.GetValue<string>().ShouldBe("vol-7");
    }

    [Fact]
    public async Task SetVariablesAndRedeploySendTheVerifiedMutations()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("variableCollectionUpsert", "{\"variableCollectionUpsert\":true}");
        harness.Api.Data("serviceInstanceRedeploy", "{\"serviceInstanceRedeploy\":true}");
        var variables = new Dictionary<string, string>(StringComparer.Ordinal) { ["KEY"] = "value-1234" };

        await harness.Client.SetVariablesAsync("svc-1", variables, skipDeploys: false, ct).ConfigureAwait(true);
        await harness.Client.RedeployAsync("svc-1", ct).ConfigureAwait(true);

        var upsert = harness.Api.Requests[0].Variables["input"]!;
        upsert["serviceId"]!.GetValue<string>().ShouldBe("svc-1");
        upsert["skipDeploys"]!.GetValue<bool>().ShouldBeFalse();
        upsert["variables"]!["KEY"]!.GetValue<string>().ShouldBe("value-1234");
        harness.Api.Requests[1].Variables["serviceId"]!.GetValue<string>().ShouldBe("svc-1");
        harness.Api.Requests[1].Variables["environmentId"]!.GetValue<string>().ShouldBe(RailwayHarness.EnvironmentId);
    }

    [Fact]
    public async Task VolumeFilesAreUnsupportedAndNoRequestIsSent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();

        var append = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await harness.Client.AppendVolumeFileAsync("vol-1", "transcript.jsonl", "line", ct).ConfigureAwait(true)).ConfigureAwait(true);
        var read = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await harness.Client.ReadVolumeFileAsync("vol-1", "transcript.jsonl", ct).ConfigureAwait(true)).ConfigureAwait(true);

        append.Failure.ShouldBe(CloudEnvironmentFailure.Unsupported);
        read.Failure.ShouldBe(CloudEnvironmentFailure.Unsupported);
        append.Message.ShouldContain("only reachable inside the worker", Case.Sensitive);
        harness.Api.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void EveryOperationTheClientSendsIsListedWithItsVerificationState()
    {
        var statuses = RailwayOperations.Statuses;

        statuses.Single(status => string.Equals(status.Operation, "serviceDelete", StringComparison.Ordinal)).Verified.ShouldBeTrue();
        statuses.Single(status => string.Equals(status.Operation, "volumeDelete", StringComparison.Ordinal)).Verified.ShouldBeTrue();
        statuses.Single(status => string.Equals(status.Operation, "deployments", StringComparison.Ordinal)).Source.ShouldContain("UNVERIFIED: introspection needed", Case.Sensitive);
        statuses.ShouldAllBe(status => status.Source.Contains("https://docs.railway.com/", StringComparison.Ordinal)
            || status.Source.StartsWith("UNVERIFIED: introspection needed", StringComparison.Ordinal));
        string[] sent =
        [
            "projectServices", "projectVolumes", "deployments", "serviceCreate", "variableCollectionUpsert", "serviceInstanceUpdate",
            "serviceInstanceRedeploy", "serviceInstanceDeployV2", "serviceDelete", "volumeCreate", "volumeUpdate", "volumeInstanceUpdate", "volumeDelete",
        ];
        foreach (var operation in sent)
        {
            statuses.Count(status => string.Equals(status.Operation, operation, StringComparison.Ordinal)).ShouldBe(1);
        }
    }

    [Fact]
    public async Task LoggerAndExceptionsNeverCarryTheTokenOrAnyVariableValue()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Data("projectServices", RailwayHarness.Services());
        harness.Api.Data("serviceCreate", "{\"serviceCreate\":{\"id\":\"svc-1\",\"name\":\"" + RailwayHarness.Label + "\"}}");
        harness.Api.Add("variableCollectionUpsert", () => RailwayFixtureHandler.Json(
            HttpStatusCode.OK,
            "{\"errors\":[{\"message\":\"rejected " + RailwayHarness.SecretValue + " and " + RailwayHarness.VariableValue + " for Bearer " + RailwayHarness.Token + "\"}]}"));

        var failure = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await harness.Client.CreateContainerAsync("s1", ct).ConfigureAwait(true)).ConfigureAwait(true);

        string[] canaries = [RailwayHarness.Token, RailwayHarness.VariableValue, RailwayHarness.SecretValue];
        foreach (var canary in canaries)
        {
            failure.Message.ShouldNotContain(canary, Case.Sensitive);
            failure.ToString().ShouldNotContain(canary, Case.Sensitive);
            harness.Logger.Entries.ShouldAllBe(entry => !entry.Message.Contains(canary, StringComparison.Ordinal));
        }

        failure.Message.ShouldContain("[REDACTED]", Case.Sensitive);
        failure.Failure.ShouldBe(CloudEnvironmentFailure.GraphQlError);
        harness.Logger.Entries.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task SetVariablesRedactsItsOwnValuesFromLaterFailures()
    {
        var ct = TestContext.Current.CancellationToken;
        using var harness = new RailwayHarness();
        harness.Api.Add("variableCollectionUpsert", () => RailwayFixtureHandler.Json(
            HttpStatusCode.OK,
            "{\"errors\":[{\"message\":\"invalid value late-secret-value-5521\"}]}"));
        var variables = new Dictionary<string, string>(StringComparer.Ordinal) { ["LATE"] = "late-secret-value-5521" };

        var failure = await Should.ThrowAsync<CloudEnvironmentException>(
            async () => await harness.Client.SetVariablesAsync("svc-1", variables, skipDeploys: true, ct).ConfigureAwait(true)).ConfigureAwait(true);

        failure.Message.ShouldNotContain("late-secret-value-5521", Case.Sensitive);
        failure.Message.ShouldContain("[REDACTED]", Case.Sensitive);
    }
}
