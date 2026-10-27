namespace OmpOS.Sandbox;

/// <summary>
/// The JSON body of a Railway GraphQL answer.
/// </summary>
/// <typeparam name="TData">The operation's data type.</typeparam>
/// <param name="Data">The data, or null when the operation failed.</param>
/// <param name="Errors">The GraphQL errors; an answer with errors is a failure even at HTTP 200.</param>
internal sealed record RailwayGraphQlResponse<TData>(TData? Data, IReadOnlyList<RailwayGraphQlError>? Errors)
    where TData : class;
