namespace Autonogrammer.Contracts.DataPlane;

/// <summary>
/// The only 503 problem a store outage produces: RFC 9457 members plus the component, a closed reason and the seconds after which a client may retry.
/// The <c>type</c> is <c>/problems/data_plane_unavailable</c>.
/// </summary>
/// <param name="Type">The problem type, <c>/problems/data_plane_unavailable</c>.</param>
/// <param name="Title">The problem title.</param>
/// <param name="Status">The HTTP status, 503.</param>
/// <param name="Detail">A fixed sentence. It never quotes a host, a connection string or a provider message.</param>
/// <param name="Component">The component that is not usable.</param>
/// <param name="Reason">The closed reason.</param>
/// <param name="RetryAfterSeconds">The seconds to wait before retrying; the same value is in the <c>Retry-After</c> header.</param>
public sealed record DataPlaneProblem(
    string Type,
    string Title,
    int Status,
    string Detail,
    DataPlaneComponent Component,
    DataPlaneReason Reason,
    int RetryAfterSeconds);
