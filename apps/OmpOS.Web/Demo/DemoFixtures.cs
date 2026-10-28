using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Catalog;
using OmpOS.Contracts.Controls;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Ui;

namespace OmpOS.Web.Demo;

/// <summary>The initial state of the Demo workspace <c>acme-shop</c>: six sessions that show every state of the rail.</summary>
internal static class DemoFixtures
{
    /// <summary>The session whose scripted turn streams while the visitor watches.</summary>
    internal const string RunningId = "5e1c0001-0000-4000-8000-00000000d001";

    /// <summary>The session that waits for a tool approval.</summary>
    internal const string ApprovalId = "5e1c0002-0000-4000-8000-00000000d002";

    /// <summary>The omp terminal session.</summary>
    internal const string TerminalId = "5e1c0003-0000-4000-8000-00000000d003";

    /// <summary>The completed session with three changed files.</summary>
    internal const string CompletedId = "5e1c0004-0000-4000-8000-00000000d004";

    /// <summary>The failed session with a retryable error.</summary>
    internal const string FailedId = "5e1c0005-0000-4000-8000-00000000d005";

    /// <summary>The closed session.</summary>
    internal const string ClosedId = "5e1c0006-0000-4000-8000-00000000d006";

    /// <summary>The request identifier of the pending tool approval.</summary>
    internal const string ApprovalRequestId = "req-demo-1";

    /// <summary>Adds the six sessions to the world.</summary>
    /// <param name="world">The world.</param>
    /// <param name="now">The current time; the sessions are created before it.</param>
    internal static void Populate(DemoWorld world, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(world);
        world.Add(Running(now));
        world.Add(WaitingForApproval(now));
        world.Add(Terminal(now));
        world.Add(Completed(now));
        world.Add(Failed(now));
        world.Add(Closed(now));
    }

    private static DemoSession Running(DateTimeOffset now)
    {
        var session = Start(RunningId, now.AddMinutes(-3), SessionStatus.Running, "graph-checkout-142");
        AddPrompt(session, "Fix the failing checkout total test and show me the diff", now.AddMinutes(-1));
        session.Stages.AddRange(DemoCatalogue.Stages(RunningId, StageStatus.Running, "Working", now.AddMinutes(-1)));
        session.Stages.Add(new StageEvent { SessionId = RunningId, Source = StageSource.Gsd, StageId = "gsd/plan-4-2", Title = "Plan 4.2 complete", Status = StageStatus.Succeeded, Timestamp = now.AddMinutes(-2), Detail = "Phase 4, plan 2 of 3.", GraphId = "graph-checkout-142" });
        session.Stages.Add(new StageEvent { SessionId = RunningId, Source = StageSource.Substrate, StageId = "substrate/lease", Title = "Task lease held", Status = StageStatus.Succeeded, Timestamp = now.AddMinutes(-2), Detail = "Held by demo-agent-1." });
        session.Options.AddRange(DemoCatalogue.Options());
        session.Context = DemoCatalogue.CheckoutContext(now);
        session.LeaseHolder = "demo-agent-1";
        return session;
    }

    private static DemoSession WaitingForApproval(DateTimeOffset now)
    {
        var session = Start(ApprovalId, now.AddMinutes(-14), SessionStatus.AwaitingToolApproval, null);
        var t = now.AddMinutes(-6);
        AddPrompt(session, "Upgrade the payments SDK to v5 and run the integration suite", t);
        session.Append(DemoEvents.Delta("I'll upgrade the package, then run the integration suite.\n\nRunning the suite executes a shell command, so I'm asking for your approval first."), t.AddSeconds(4));
        session.Append(
            DemoEvents.Block(new ToolCallBlock
            {
                Id = "call-approval",
                CallId = "call-approval",
                ToolName = "bash",
                Status = "running",
                Arguments = DemoEvents.Json("""{"command":"dotnet add package Acme.Payments --version 5.0.2 && dotnet test tests/Payments.IntegrationTests"}"""),
            }),
            t.AddSeconds(6));
        session.Append(DemoEvents.ApprovalRequested(ApprovalRequestId, "bash"), t.AddSeconds(7));
        session.Approvals.Add(new PendingToolApproval { RequestId = ApprovalRequestId, ToolName = "bash", RequestedAt = t.AddSeconds(7), GrantableTools = ["bash"] });
        session.Stages.AddRange(DemoCatalogue.Stages(ApprovalId, StageStatus.Running, "Waiting for your approval", t.AddSeconds(7)));
        session.Options.AddRange(DemoCatalogue.Options());
        return session;
    }

    private static DemoSession Terminal(DateTimeOffset now)
    {
        var session = Start(TerminalId, now.AddMinutes(-20), SessionStatus.Running, null, CatalogSessionKind.Terminal);
        session.UpdatedAt = now.AddMinutes(-8);
        return session;
    }

    private static DemoSession Completed(DateTimeOffset now)
    {
        var session = Start(CompletedId, now.AddHours(-2), SessionStatus.Completed, "graph-products-137");
        var t = now.AddMinutes(-35);
        AddPrompt(session, "Add pagination to the products API", t);
        session.Append(DemoEvents.Delta("I'll add `page` and `pageSize` parameters, return a `PagedResult<T>`, and cover it with tests."), t.AddSeconds(5));
        var edits = new[]
        {
            (Path: "src/Products/ProductsController.cs", Old: "public IActionResult List() => Ok(_store.All());\n", New: "public IActionResult List(int page = 1, int pageSize = 20) => Ok(_store.Page(page, pageSize));\n"),
            (Path: "src/Products/PagedResult.cs", Old: (string?)null, New: "public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);\n"),
            (Path: "tests/Products/ProductsPagingTests.cs", Old: (string?)null, New: "[Fact]\npublic void SecondPageHoldsTheNextTwentyProducts() => Assert.Equal(20, Store.Page(2, 20).Items.Count);\n"),
        };
        var seconds = 10;
        foreach (var edit in edits)
        {
            session.Append(DemoEvents.Block(new FileEditBlock { Id = "edit-" + seconds, CallId = "call-" + seconds, Path = edit.Path, OldText = edit.Old, NewText = edit.New }), t.AddSeconds(seconds));
            session.Changes.Add(new SessionChange
            {
                Path = edit.Path,
                Kind = edit.Old is null ? ChangeKind.Added : ChangeKind.Modified,
                Status = ChangeReviewStatus.Pending,
                OldText = edit.Old,
                NewText = edit.New,
            });
            seconds += 6;
        }

        session.Append(DemoEvents.Delta("The endpoint is paged and three files changed. Review them in the Changes bar."), t.AddSeconds(40));
        session.Append(DemoEvents.Completed(), t.AddSeconds(42));
        session.Stages.AddRange(DemoCatalogue.Stages(CompletedId, StageStatus.Succeeded, "Finished", t.AddSeconds(42)));
        session.Options.AddRange(DemoCatalogue.Options(mode: "plan"));
        session.Context = DemoCatalogue.CheckoutContext(now) with
        {
            Title = "Add pagination to the products API",
            TaskId = "TASK-137",
            GraphId = "graph-products-137",
            KanbanStatus = "review",
            Phase = new GsdPhaseInfo { Number = "3", Name = "Catalogue", Status = "verifying", PlansComplete = 3, PlansTotal = 3 },
        };
        session.LeaseHolder = "demo-agent-2";
        return session;
    }

    private static DemoSession Failed(DateTimeOffset now)
    {
        var session = Start(FailedId, now.AddHours(-3), SessionStatus.Failed, null);
        var t = now.AddHours(-2);
        AddPrompt(session, "Generate the changelog for release 2.4", t);
        session.Append(DemoEvents.Delta("Reading the merged pull requests since 2.3..."), t.AddSeconds(3));
        session.Append(DemoEvents.Error("The model provider timed out (scripted Demo error). Try again.", retryable: true), t.AddSeconds(30));
        session.Stages.AddRange(DemoCatalogue.Stages(FailedId, StageStatus.Failed, "The model provider timed out.", t.AddSeconds(30)));
        session.Options.AddRange(DemoCatalogue.Options());
        return session;
    }

    private static DemoSession Closed(DateTimeOffset now)
    {
        var session = Start(ClosedId, now.AddDays(-2), SessionStatus.Completed, null);
        var t = now.AddDays(-1);
        AddPrompt(session, "Rename OrderRepo to OrderStore across the solution", t);
        session.Append(DemoEvents.Delta("Renamed `OrderRepo` to `OrderStore` in 14 files and updated the registrations. The build passes."), t.AddSeconds(20));
        session.Append(DemoEvents.Completed(), t.AddSeconds(22));
        session.Stages.AddRange(DemoCatalogue.Stages(ClosedId, StageStatus.Succeeded, "Finished", t.AddSeconds(22)));
        session.Options.AddRange(DemoCatalogue.Options());
        session.Closed = true;
        return session;
    }

    private static DemoSession Start(string id, DateTimeOffset createdAt, SessionStatus status, string? graphId, CatalogSessionKind kind = CatalogSessionKind.Agent) =>
        new() { Id = id, CreatedAt = createdAt, UpdatedAt = createdAt, Status = status, GraphId = graphId, Kind = kind };

    private static void AddPrompt(DemoSession session, string prompt, DateTimeOffset at)
    {
        session.Messages.Add(new Message { Role = "user", Content = prompt, Timestamp = at });
        session.Append(DemoEvents.Prompt(prompt), at);
    }
}
