namespace OmpOS.Sandbox;

/// <summary>
/// The JSON body of a Railway GraphQL POST.
/// </summary>
/// <typeparam name="TVariables">The operation's variables type.</typeparam>
/// <param name="Query">The GraphQL document.</param>
/// <param name="Variables">The variables.</param>
internal sealed record RailwayGraphQlRequest<TVariables>(string Query, TVariables Variables);
