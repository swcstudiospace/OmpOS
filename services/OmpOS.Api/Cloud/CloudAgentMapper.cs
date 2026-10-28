using System.Security.Cryptography;
using System.Text;
using OmpOS.Contracts.Cloud;

namespace OmpOS.Api.Cloud;

/// <summary>Builds the public view of an agent from its record. The view carries no provider identifier: the worker reference is a one-way hash.</summary>
internal static class CloudAgentMapper
{
    /// <summary>Maps a record to its response.</summary>
    /// <param name="record">The record.</param>
    /// <param name="now">The current instant, used to include the open awake stretch in the compute seconds.</param>
    /// <returns>The response.</returns>
    public static CloudAgentResponse ToResponse(CloudAgentRecord record, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(record);
        var open = record.ActiveSince is { } since ? Math.Max(0, (now - since).TotalSeconds) : 0;
        return new CloudAgentResponse
        {
            Id = record.Id,
            Name = record.Name,
            Status = record.Status,
            CreatedAt = record.CreatedAt,
            Repositories = record.Repositories,
            WorkerRef = record.WorkerRef is null ? null : OpaqueRef(record.WorkerRef),
            Branch = record.Branch,
            SessionId = record.SessionId,
            PullRequests = record.PullRequests,
            PullRequest = record.PullRequests.Count == 0 ? null : record.PullRequests[^1],
            LastActivityAt = record.LastActivityAt,
            IdleAt = record.IdleAt,
            IdleMinutes = record.IdleMinutes,
            ComputeSeconds = record.ComputeSeconds + open,
            SecretRefs = record.SecretRefs,
            Model = record.Model,
            Mode = record.Mode,
            Error = record.Error,
        };
    }

    /// <summary>Hides a provider identifier behind a stable opaque reference.</summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <returns><c>wrk_</c> and sixteen hexadecimal characters of the SHA-256 of the identifier.</returns>
    public static string OpaqueRef(string providerId)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        return "wrk_" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(providerId)))[..16];
    }
}
