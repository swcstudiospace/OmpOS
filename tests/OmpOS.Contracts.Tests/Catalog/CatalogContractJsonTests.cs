using System.Text.Json;
using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Catalog;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using Shouldly;
using Xunit;

namespace OmpOS.Contracts.Tests.Catalog;

/// <summary>Verifies the catalog shapes of contract 1.3.0: wire names, a byte-stable round trip through <see cref="CatalogJsonContext"/>, and the additive catalog fields.</summary>
public sealed class CatalogContractJsonTests
{
    private static readonly DateTimeOffset Stamp = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A row keeps the stored session's members and adds the catalog members, and reads back equal.</summary>
    [Fact]
    public void CatalogEntry_RoundTrips_WithSessionAndCatalogMembers()
    {
        var entry = new SessionCatalogEntry
        {
            Id = "s1",
            OwnerId = "alice",
            CreatedAt = Stamp,
            UpdatedAt = Stamp,
            Status = SessionStatus.AwaitingToolApproval,
            Messages = [new Message { Role = "user", Content = "hello", Timestamp = Stamp }],
            Events = [new StreamEvent { Seq = 1, SessionId = "s1", CorrelationId = "s1/turn-1", Timestamp = Stamp, Payload = new TextDeltaPayload { Delta = "hi" } }],
            Kind = CatalogSessionKind.Cloud,
            RowStatus = CatalogRowStatus.NeedsApproval,
            Group = CatalogRowGroup.NeedsYou,
            LastActivityAt = Stamp,
            GraphId = "g1",
            Closed = true,
            WorkspaceLabel = "OmpOS",
        };

        var json = JsonSerializer.Serialize(entry, CatalogJsonContext.Default.SessionCatalogEntry);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.GetProperty("status").GetString().ShouldBe("AwaitingToolApproval", Case.Sensitive);
        root.GetProperty("kind").GetString().ShouldBe("cloud", Case.Sensitive);
        root.GetProperty("rowStatus").GetString().ShouldBe("needs-approval", Case.Sensitive);
        root.GetProperty("group").GetString().ShouldBe("needs-you", Case.Sensitive);
        root.GetProperty("events")[0].GetProperty("payload").GetProperty("kind").GetString().ShouldBe("text_delta", Case.Sensitive);
        var back = JsonSerializer.Deserialize(json, CatalogJsonContext.Default.SessionCatalogEntry).ShouldNotBeNull();
        back.Kind.ShouldBe(CatalogSessionKind.Cloud);
        back.Events.ShouldHaveSingleItem().Payload.ShouldBeOfType<TextDeltaPayload>();
        JsonSerializer.Serialize(back, CatalogJsonContext.Default.SessionCatalogEntry).ShouldBe(json, Case.Sensitive);
    }

    /// <summary>A row reads back as a plain session: a client of the older contract keeps working.</summary>
    [Fact]
    public void CatalogEntry_ReadsAsAStoredSession()
    {
        var entry = new SessionCatalogEntry
        {
            Id = "s1",
            OwnerId = "alice",
            CreatedAt = Stamp,
            UpdatedAt = Stamp,
            Status = SessionStatus.Idle,
            Messages = [],
            Events = [],
            Kind = CatalogSessionKind.Agent,
            RowStatus = CatalogRowStatus.Ready,
            Group = CatalogRowGroup.Idle,
            LastActivityAt = Stamp,
            Closed = false,
        };

        var json = JsonSerializer.Serialize(entry, CatalogJsonContext.Default.SessionCatalogEntry);
        var asSession = JsonSerializer.Deserialize(json, OmpOS.Contracts.Serialization.OmpOsJsonContext.Default.Session).ShouldNotBeNull();

        asSession.Id.ShouldBe("s1", Case.Sensitive);
        asSession.Status.ShouldBe(SessionStatus.Idle);
        json.ShouldNotContain("graphId", Case.Sensitive);
    }

    /// <summary>Every enum travels under its documented wire name, and an unknown name is a JSON error rather than a silent default.</summary>
    [Theory]
    [InlineData(CatalogRowStatus.NeedsApproval, "needs-approval")]
    [InlineData(CatalogRowStatus.WaitingForYou, "waiting-for-you")]
    [InlineData(CatalogRowStatus.Unknown, "unknown")]
    public void RowStatus_UsesItsWireName(CatalogRowStatus status, string wire)
    {
        var confirmation = new CatalogChangedEvent { SessionId = "s", Change = CatalogChangeKind.Reopened, RowStatus = status, Group = CatalogRowGroup.Finished };

        var json = JsonSerializer.Serialize(confirmation, CatalogJsonContext.Default.CatalogChangedEvent);

        json.ShouldBe($$"""{"sessionId":"s","change":"reopened","rowStatus":"{{wire}}","group":"finished"}""", Case.Sensitive);
        JsonSerializer.Deserialize(json, CatalogJsonContext.Default.CatalogChangedEvent).ShouldBe(confirmation);
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize("""{"sessionId":"s","change":"vanished","rowStatus":"ready","group":"idle"}""", CatalogJsonContext.Default.CatalogChangedEvent));
    }

    /// <summary>The board, workspace, source, stage and context answers round trip.</summary>
    [Fact]
    public void BoardWorkspaceSourceStagesAndContext_RoundTrip()
    {
        var board = new BoardResponse
        {
            State = BoardStateKind.Ready,
            Columns = [new BoardColumn { Key = "backlog", Title = "Backlog", AcceptsDrop = true, Cards = [new BoardCard { Id = "s1", Title = "Agent s1", RowStatus = CatalogRowStatus.Ready, ColumnKey = "backlog", LastActivityAt = Stamp, IsStale = false, Closed = false }] }],
        };
        var workspace = new WorkspaceInfo { Name = "OmpOS", FolderLabel = "OmpOS", PluginCheck = new PluginCheckResult { Status = PluginCheckStatus.Degraded, Summary = "One plugin is missing." } };
        var source = new SourceInfo { Licence = "AGPL-3.0-or-later", LicenceUrl = "https://www.gnu.org/licenses/agpl-3.0.html", SourceUrl = "https://github.com/swcstudiospace/OmpOS", Revision = "unknown", Version = "0.15.0.0" };
        var stages = new SessionStagesResponse
        {
            SessionId = "s1",
            Available = false,
            Reason = "none",
            Stages = [new StageEvent { SessionId = "s1", Source = StageSource.Omp, StageId = "turn", Title = "Engine turn", Status = StageStatus.Running, Timestamp = Stamp }],
        };
        var context = new SessionContextResponse
        {
            SessionId = "s1",
            Available = true,
            Context = new TaskContext { Title = "T", SharedMemory = [], Lessons = [new ContextNote { Id = "l", Text = "x", Origin = "o" }], Warnings = [] },
        };

        JsonSerializer.Serialize(board, CatalogJsonContext.Default.BoardResponse).ShouldContain("\"state\":\"ready\"", Case.Sensitive);
        JsonSerializer.Deserialize(JsonSerializer.Serialize(board, CatalogJsonContext.Default.BoardResponse), CatalogJsonContext.Default.BoardResponse).ShouldNotBeNull().Columns.ShouldHaveSingleItem().Cards.ShouldHaveSingleItem().Id.ShouldBe("s1", Case.Sensitive);
        JsonSerializer.Serialize(workspace, CatalogJsonContext.Default.WorkspaceInfo).ShouldContain("\"status\":\"degraded\"", Case.Sensitive);
        JsonSerializer.Serialize(source, CatalogJsonContext.Default.SourceInfo)
            .ShouldBe("""{"licence":"AGPL-3.0-or-later","licenceUrl":"https://www.gnu.org/licenses/agpl-3.0.html","sourceUrl":"https://github.com/swcstudiospace/OmpOS","revision":"unknown","version":"0.15.0.0"}""", Case.Sensitive);
        JsonSerializer.Deserialize(JsonSerializer.Serialize(stages, CatalogJsonContext.Default.SessionStagesResponse), CatalogJsonContext.Default.SessionStagesResponse).ShouldNotBeNull().Stages.ShouldHaveSingleItem().Status.ShouldBe(StageStatus.Running);
        JsonSerializer.Deserialize(JsonSerializer.Serialize(context, CatalogJsonContext.Default.SessionContextResponse), CatalogJsonContext.Default.SessionContextResponse).ShouldNotBeNull().Context.ShouldNotBeNull().Lessons.ShouldHaveSingleItem().Id.ShouldBe("l", Case.Sensitive);
    }
}
