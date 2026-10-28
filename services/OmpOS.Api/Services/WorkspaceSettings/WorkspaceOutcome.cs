using Microsoft.AspNetCore.Http.HttpResults;

namespace OmpOS.Api.Services.WorkspaceSettings;

/// <summary>The result of a workspace read: the value, or the problem the endpoint answers with.</summary>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="Value">The value when the read succeeded.</param>
/// <param name="Problem">The problem when the read failed, otherwise null.</param>
internal readonly record struct WorkspaceOutcome<T>(T? Value, ProblemHttpResult? Problem)
{
    /// <summary>Creates a successful outcome.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The outcome.</returns>
    public static WorkspaceOutcome<T> Success(T value) => new(value, null);

    /// <summary>Creates a failed outcome.</summary>
    /// <param name="problem">The problem to answer with.</param>
    /// <returns>The outcome.</returns>
    public static WorkspaceOutcome<T> Failure(ProblemHttpResult problem) => new(default, problem);
}
