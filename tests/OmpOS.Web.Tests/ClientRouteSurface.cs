using OmpOS.Contracts.Controls;
using OmpOS.Web.Demo;

namespace OmpOS.Web.Tests;

/// <summary>Calls every route of <see cref="OmpOS.Web.Services.IOmpApiClient"/> and the user event feed once, against the Demo, and returns the normalised routes the client used.</summary>
internal static class ClientRouteSurface
{
    /// <summary>Calls every route.</summary>
    /// <param name="host">The Demo host. The calls change its state.</param>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>The routes, as <c>GET /sessions/{}/stages</c>: every variable segment is <c>{}</c>.</returns>
    internal static async Task<IReadOnlyList<string>> ExerciseAsync(DemoTestHost host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        var api = host.Api;
        _ = await api.ListSessionsAsync(cancellationToken).ConfigureAwait(true);
        _ = await api.GetWorkspaceAsync(cancellationToken).ConfigureAwait(true);
        _ = await api.GetSourceAsync(cancellationToken).ConfigureAwait(true);
        _ = await api.IsReadyAsync(cancellationToken).ConfigureAwait(true);
        _ = await api.GetBoardAsync(cancellationToken).ConfigureAwait(true);
        _ = await api.GetSessionAsync(DemoFixtures.RunningId, cancellationToken).ConfigureAwait(true);
        _ = await api.GetStagesAsync(DemoFixtures.RunningId, cancellationToken).ConfigureAwait(true);
        _ = await api.GetContextAsync(DemoFixtures.RunningId, cancellationToken).ConfigureAwait(true);
        _ = await api.GetConfigOptionsAsync(DemoFixtures.RunningId, cancellationToken).ConfigureAwait(true);
        _ = await api.GetCommandsAsync(DemoFixtures.RunningId, cancellationToken).ConfigureAwait(true);
        _ = await api.GetChangesAsync(DemoFixtures.CompletedId, cancellationToken).ConfigureAwait(true);
        _ = await api.GetCardDetailsAsync(DemoFixtures.RunningId, cancellationToken).ConfigureAwait(true);
        _ = await api.GetToolApprovalsAsync(DemoFixtures.ApprovalId, cancellationToken).ConfigureAwait(true);
        await foreach (var unused in api.StreamSessionAsync(DemoFixtures.CompletedId, null, cancellationToken).ConfigureAwait(true))
        {
            _ = unused;
        }

        var created = await api.CreateSessionAsync(cancellationToken).ConfigureAwait(true);
        _ = await api.SetConfigOptionAsync(created.Id, "model", "openai/gpt-5-codex", cancellationToken).ConfigureAwait(true);
        await api.SubmitPromptAsync(created.Id, "hello", cancellationToken).ConfigureAwait(true);
        _ = await api.SteerAsync(created.Id, "use a smaller change", SteerMode.SendNow, cancellationToken).ConfigureAwait(true);
        await api.DecideToolApprovalAsync(DemoFixtures.ApprovalId, DemoFixtures.ApprovalRequestId, allow: true, cancellationToken).ConfigureAwait(true);
        _ = await api.DecideChangeAsync(DemoFixtures.CompletedId, "src/Products/ProductsController.cs", accept: true, cancellationToken).ConfigureAwait(true);
        _ = await api.DecideChangeAsync(DemoFixtures.CompletedId, "src/Products/PagedResult.cs", accept: false, cancellationToken).ConfigureAwait(true);
        _ = await api.CloseSessionAsync(DemoFixtures.ClosedId, cancellationToken).ConfigureAwait(true);
        _ = await api.ReopenSessionAsync(DemoFixtures.ClosedId, cancellationToken).ConfigureAwait(true);
        try
        {
            _ = await api.ResumeSessionAsync(DemoFixtures.FailedId, cancellationToken).ConfigureAwait(true);
        }
        catch (OmpOS.Web.Services.ApiProblemException problem) when (problem.Kind == OmpOS.Web.Services.ApiProblemKind.Conflict)
        {
            // No Demo session is lost, so resume answers the conflict the real API gives for a session that is not lost; the route exists.
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var enumerator = host.Feed.StreamAsync(null, cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        var first = enumerator.MoveNextAsync();
        _ = await api.CloseSessionAsync(DemoFixtures.FailedId, cancellationToken).ConfigureAwait(true);
        _ = await first.ConfigureAwait(true);
        await cancellation.CancelAsync().ConfigureAwait(true);
        try
        {
            _ = await enumerator.MoveNextAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // The stream ended with the token.
        }

        await enumerator.DisposeAsync().ConfigureAwait(true);
        return Normalise(host.Recorder.Requests);
    }

    /// <summary>Replaces every variable segment of recorded requests with <c>{}</c>, and drops duplicates.</summary>
    /// <param name="requests">The recorded <c>METHOD /path</c> strings.</param>
    /// <returns>The distinct routes in first-use order.</returns>
    internal static IReadOnlyList<string> Normalise(IEnumerable<string> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        var routes = new List<string>();
        foreach (var request in requests)
        {
            var space = request.IndexOf(' ', StringComparison.Ordinal);
            var segments = request[(space + 1)..].Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2 && string.Equals(segments[0], "sessions", StringComparison.Ordinal))
            {
                segments[1] = "{}";
                if (segments.Length == 4 && segments[2] is "tool-approvals" or "config-options")
                {
                    segments[3] = "{}";
                }

                if (segments.Length >= 5 && string.Equals(segments[2], "changes", StringComparison.Ordinal))
                {
                    segments[3] = "{}";
                }
            }

            var route = request[..space] + " /" + string.Join('/', segments);
            if (!routes.Contains(route, StringComparer.Ordinal))
            {
                routes.Add(route);
            }
        }

        return routes;
    }
}
