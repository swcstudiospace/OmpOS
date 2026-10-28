using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Api.Cloud;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.Cloud;

/// <summary>Orphans and repairs: a record whose worker is gone, a worker no record claims (reported, and removed only with the label and only when cleanup is on), and a deletion the provider refused at first.</summary>
public sealed class CloudOrphanTests
{
    /// <summary>A record that names a worker the provider no longer has is marked orphaned by the sweep, and a prompt to it says so.</summary>
    [Fact]
    public async Task RecordWithoutWorker_IsMarkedOrphaned()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;
        host.Environment.Hide(agent.GetProperty("sessionId").GetString()!);

        var swept = await host.SweepAsync(ct).ConfigureAwait(true);

        swept.Orphans.ShouldNotBeNull().RecordsWithoutWorker.ToArray().ShouldBe([id]);
        var orphaned = await host.WaitForStatusAsync(id, "orphaned", cancellationToken: ct).ConfigureAwait(true);
        orphaned.GetProperty("error").GetProperty("code").GetString().ShouldBe("worker_missing");
        using var refused = await host.PromptAsync(id, "hello", cancellationToken: ct).ConfigureAwait(true);
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CloudApiHost.ProblemCodeAsync(refused, ct).ConfigureAwait(true)).ShouldBe("agent_orphaned");
        host.Worker.Prompts.ShouldBeEmpty();
    }

    /// <summary>A labelled worker with no record is reported but not removed unless cleanup is on; a worker without the label, and a labelled worker whose agent exists, are never removed.</summary>
    [Fact]
    public async Task WorkerWithoutRecord_IsRemovedOnlyWithTheLabelAndOnlyWhenCleanupIsOn()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;
        host.Environment.ExtraWorkers.Add(new CloudWorkerInfo("ghost-ctr", "ghost-session", "ghost-vol", new Dictionary<string, string>(StringComparer.Ordinal) { [CloudWorkerLabels.AgentKey] = "ghost-agent" }));
        host.Environment.ExtraWorkers.Add(new CloudWorkerInfo("foreign-ctr", "foreign-session", null, new Dictionary<string, string>(StringComparer.Ordinal)));
        host.Environment.ExtraWorkers.Add(new CloudWorkerInfo("live-ctr", "live-session", null, new Dictionary<string, string>(StringComparer.Ordinal) { [CloudWorkerLabels.AgentKey] = id }));
        var scanner = host.Services.GetRequiredService<CloudOrphanScanner>();

        var report = await scanner.ScanAsync(cleanup: false, ct).ConfigureAwait(true);

        report.InventoryAvailable.ShouldBeTrue();
        report.WorkersWithoutRecord.ToArray().ShouldBe(["ghost-ctr"]);
        report.WorkersRemoved.ShouldBeEmpty();
        report.UnlabelledWorkers.ShouldBe(1);
        host.Environment.Removed.ShouldBeEmpty();

        var cleaned = await scanner.ScanAsync(cleanup: true, ct).ConfigureAwait(true);

        cleaned.WorkersRemoved.ToArray().ShouldBe(["ghost-ctr"]);
        host.Environment.Removed.ShouldHaveSingleItem().ContainerId.ShouldBe("ghost-ctr");
        host.Environment.ExtraWorkers.Select(static worker => worker.ContainerId).Order(StringComparer.Ordinal).ToArray().ShouldBe(["foreign-ctr", "live-ctr"]);
    }

    /// <summary>The sweep applies the cleanup setting: with it on, an orphan worker is removed by the periodic scan.</summary>
    [Fact]
    public async Task Sweep_RunsTheOrphanScanAtItsIntervalAndHonoursCleanup()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Cloud:Sweeper:OrphanCleanup"] = "true" }, ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        host.Environment.ExtraWorkers.Add(new CloudWorkerInfo("ghost-ctr", "ghost-session", null, new Dictionary<string, string>(StringComparer.Ordinal) { [CloudWorkerLabels.AgentKey] = "ghost-agent" }));

        var first = await host.SweepAsync(ct).ConfigureAwait(true);
        first.Orphans.ShouldNotBeNull().WorkersRemoved.ToArray().ShouldBe(["ghost-ctr"]);
        var second = await host.SweepAsync(ct).ConfigureAwait(true);
        second.Orphans.ShouldBeNull();
        host.Clock.Advance(TimeSpan.FromMinutes(11));
        var third = await host.SweepAsync(ct).ConfigureAwait(true);

        third.Orphans.ShouldNotBeNull().WorkersWithoutRecord.ShouldBeEmpty();
    }

    /// <summary>A provider that refuses the worker removal leaves the agent in deleting with a 502; the sweep finishes the deletion later.</summary>
    [Fact]
    public async Task Delete_RetriedBySweepWhenTheProviderRefusedFirst()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = await CloudApiHost.StartAsync(cancellationToken: ct).ConfigureAwait(true);
        await using var hostScope = host.ConfigureAwait(true);
        var agent = await host.CreateRunningAgentAsync(cancellationToken: ct).ConfigureAwait(true);
        var id = agent.GetProperty("id").GetString()!;
        host.Environment.FailRemoveTimes = 1;
        using var client = host.Client();

        using var refused = await client.DeleteAsync(new Uri($"/cloud/agents/{id}", UriKind.Relative), ct).ConfigureAwait(true);

        refused.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        (await CloudApiHost.ProblemCodeAsync(refused, ct).ConfigureAwait(true)).ShouldBe("worker_removal_failed");
        _ = await host.WaitForStatusAsync(id, "deleting", cancellationToken: ct).ConfigureAwait(true);
        using var prompt = await host.PromptAsync(id, "no", cancellationToken: ct).ConfigureAwait(true);
        prompt.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var swept = await host.SweepAsync(ct).ConfigureAwait(true);

        swept.DeletionsCompleted.ShouldBe(1);
        using var gone = await client.GetAsync(new Uri($"/cloud/agents/{id}", UriKind.Relative), ct).ConfigureAwait(true);
        gone.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        host.Environment.Removed.ShouldHaveSingleItem().Labels[CloudWorkerLabels.AgentKey].ShouldBe(id);
    }
}
