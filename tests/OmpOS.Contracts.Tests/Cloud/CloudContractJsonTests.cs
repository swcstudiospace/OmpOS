using System.Text.Json;
using OmpOS.Contracts.Cloud;
using Shouldly;
using Xunit;

namespace OmpOS.Contracts.Tests.Cloud;

/// <summary>The cloud agent shapes: wire names, a byte-stable round trip through <see cref="CloudJsonContext"/>, tolerance of unknown members, refusal of unknown statuses, and the absence of anything secret from the public shapes.</summary>
public sealed class CloudContractJsonTests
{
    private static readonly DateTimeOffset Stamp = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Every status has its documented wire name and reads back to the same member.</summary>
    [Theory]
    [InlineData(CloudAgentStatus.Provisioning, "provisioning")]
    [InlineData(CloudAgentStatus.Starting, "starting")]
    [InlineData(CloudAgentStatus.Running, "running")]
    [InlineData(CloudAgentStatus.Idle, "idle")]
    [InlineData(CloudAgentStatus.Stopping, "stopping")]
    [InlineData(CloudAgentStatus.Stopped, "stopped")]
    [InlineData(CloudAgentStatus.Failed, "failed")]
    [InlineData(CloudAgentStatus.Deleting, "deleting")]
    [InlineData(CloudAgentStatus.Orphaned, "orphaned")]
    [InlineData(CloudAgentStatus.LimitReached, "limit_reached")]
    public void Status_HasItsWireName(CloudAgentStatus status, string wire)
    {
        var json = JsonSerializer.Serialize(status, CloudJsonContext.Default.CloudAgentStatus);

        json.ShouldBe("\"" + wire + "\"");
        JsonSerializer.Deserialize("\"" + wire + "\"", CloudJsonContext.Default.CloudAgentStatus).ShouldBe(status);
    }

    /// <summary>An unknown status is a deserialization failure the caller reports, never a silent default.</summary>
    [Fact]
    public void UnknownStatus_IsRefused()
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize("\"hibernating\"", CloudJsonContext.Default.CloudAgentStatus));
    }

    /// <summary>A response round trips byte for byte, with camel-case members and no null members.</summary>
    [Fact]
    public void Response_RoundTrips()
    {
        var response = new CloudAgentResponse
        {
            Id = "a1",
            Name = "acme/widgets",
            Status = CloudAgentStatus.Running,
            CreatedAt = Stamp,
            Repositories = [new CloudRepository { Address = "https://github.com/acme/widgets", Branch = "main" }],
            WorkerRef = "wrk_0123456789abcdef",
            Branch = "ompos/cloud/a1",
            SessionId = "cloud-1",
            PullRequests = [new CloudPullRequest { Repository = "https://github.com/acme/widgets", Number = 7, Url = new Uri("https://github.com/acme/widgets/pull/7", UriKind.Absolute), Branch = "ompos/cloud/a1", BaseBranch = "main", Draft = false, CreatedAt = Stamp }],
            LastActivityAt = Stamp,
            IdleMinutes = 15,
            ComputeSeconds = 12.5,
            SecretRefs = ["API_KEY"],
        };

        var json = JsonSerializer.Serialize(response, CloudJsonContext.Default.CloudAgentResponse);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.GetProperty("status").GetString().ShouldBe("running");
        root.GetProperty("workerRef").GetString().ShouldBe("wrk_0123456789abcdef");
        root.GetProperty("pullRequests")[0].GetProperty("baseBranch").GetString().ShouldBe("main");
        root.TryGetProperty("idleAt", out _).ShouldBeFalse();
        root.TryGetProperty("error", out _).ShouldBeFalse();
        root.TryGetProperty("model", out _).ShouldBeFalse();
        var back = JsonSerializer.Deserialize(json, CloudJsonContext.Default.CloudAgentResponse).ShouldNotBeNull();
        back.Status.ShouldBe(CloudAgentStatus.Running);
        back.SecretRefs.ToArray().ShouldBe(["API_KEY"]);
        JsonSerializer.Serialize(back, CloudJsonContext.Default.CloudAgentResponse).ShouldBe(json);
    }

    /// <summary>The create request reads the documented members, tolerates unknown ones, and keeps absent members null so the server can apply its defaults.</summary>
    [Fact]
    public void CreateRequest_ReadsDocumentedMembersAndIgnoresUnknownOnes()
    {
        const string json =
            "{\"name\":\"n\",\"repositories\":[{\"url\":\"https://github.com/acme/widgets\",\"branch\":\"dev\",\"baseBranch\":\"main\"}]," +
            "\"environmentId\":\"node\",\"secretRefs\":[\"A\",\"B\"],\"idleMinutes\":5,\"model\":\"m\",\"mode\":\"plan\",\"futureMember\":{\"x\":1}}";

        var request = JsonSerializer.Deserialize(json, CloudJsonContext.Default.CreateCloudAgentRequest).ShouldNotBeNull();

        request.Name.ShouldBe("n");
        request.Repositories.ShouldNotBeNull().ShouldHaveSingleItem().BaseBranch.ShouldBe("main");
        request.SecretRefs.ShouldNotBeNull().ToArray().ShouldBe(["A", "B"]);
        request.IdleMinutes.ShouldBe(5);
        var empty = JsonSerializer.Deserialize("{}", CloudJsonContext.Default.CreateCloudAgentRequest).ShouldNotBeNull();
        empty.Repositories.ShouldBeNull();
        empty.IdleMinutes.ShouldBeNull();
        empty.SecretRefs.ShouldBeNull();
    }

    /// <summary>The stored record round trips with every member, including the provider references that the public view never shows.</summary>
    [Fact]
    public void Record_RoundTripsForTheStore()
    {
        var record = new CloudAgentRecord
        {
            Id = "a1",
            OwnerId = "alice",
            SessionId = "cloud-1",
            Name = "n",
            Status = CloudAgentStatus.Idle,
            CreatedAt = Stamp,
            UpdatedAt = Stamp,
            Repositories = [new CloudRepository { Address = "https://github.com/acme/widgets" }],
            Branch = "ompos/cloud/a1",
            IdleMinutes = 15,
            LastActivityAt = Stamp,
            SecretRefs = ["API_KEY"],
            WorkerRef = "ctr-1",
            VolumeRef = "vol-1",
            NativeSessionId = "acp-1",
            IdleAt = Stamp,
            ActiveSince = null,
            ComputeSeconds = 3.25,
            Error = new CloudAgentError { Code = "wake_timeout", Message = "m" },
            IdempotencyKey = "k",
            RequestFingerprint = "f",
        };

        var json = JsonSerializer.Serialize(record, CloudJsonContext.Default.CloudAgentRecord);
        var back = JsonSerializer.Deserialize(json, CloudJsonContext.Default.CloudAgentRecord).ShouldNotBeNull();

        new CloudRecordComparer().Equals(back, record).ShouldBeTrue();
        back.Error.ShouldNotBeNull().Code.ShouldBe("wake_timeout");
        back.WorkerRef.ShouldBe("ctr-1");
    }

    /// <summary>The public shapes have no member that could hold a secret value, a token or a provider identifier.</summary>
    [Fact]
    public void PublicShapes_HaveNoSecretBearingMember()
    {
        var shapes = new[] { typeof(CloudAgentResponse), typeof(CloudSecretInfo), typeof(CloudSecretListResponse), typeof(CloudLimitsResponse), typeof(CloudPromptAccepted), typeof(CloudPullRequest) };
        var names = shapes.SelectMany(static shape => shape.GetProperties()).Select(static property => property.Name).ToArray();

        names.ShouldNotContain("Value", StringComparer.Ordinal);
        names.ShouldNotContain("Token", StringComparer.Ordinal);
        names.ShouldNotContain("VolumeRef", StringComparer.Ordinal);
        names.ShouldNotContain("NativeSessionId", StringComparer.Ordinal);
        names.ShouldNotContain("IdempotencyKey", StringComparer.Ordinal);
        typeof(CloudAgentResponse).GetProperty("WorkerRef")!.PropertyType.ShouldBe(typeof(string));
    }

    private sealed class CloudRecordComparer : IEqualityComparer<CloudAgentRecord>
    {
        public bool Equals(CloudAgentRecord? x, CloudAgentRecord? y) =>
            x is not null && y is not null
            && string.Equals(x.Id, y.Id, StringComparison.Ordinal) && string.Equals(x.OwnerId, y.OwnerId, StringComparison.Ordinal)
            && string.Equals(x.SessionId, y.SessionId, StringComparison.Ordinal) && x.Status == y.Status
            && x.UpdatedAt == y.UpdatedAt && string.Equals(x.WorkerRef, y.WorkerRef, StringComparison.Ordinal)
            && string.Equals(x.VolumeRef, y.VolumeRef, StringComparison.Ordinal) && string.Equals(x.NativeSessionId, y.NativeSessionId, StringComparison.Ordinal)
            && x.ComputeSeconds == y.ComputeSeconds && x.IdleAt == y.IdleAt && string.Equals(x.IdempotencyKey, y.IdempotencyKey, StringComparison.Ordinal)
            && string.Equals(x.RequestFingerprint, y.RequestFingerprint, StringComparison.Ordinal)
            && x.SecretRefs.SequenceEqual(y.SecretRefs, StringComparer.Ordinal)
            && x.Repositories.Select(static repository => repository.Address).SequenceEqual(y.Repositories.Select(static repository => repository.Address), StringComparer.Ordinal);

        public int GetHashCode(CloudAgentRecord obj) => obj.Id.GetHashCode(StringComparison.Ordinal);
    }
}
