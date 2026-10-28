namespace OmpOS.Web.Services;

/// <summary>Turns a failed call into one short sentence for the UI. The sentence never holds a prompt, a request body or a credential.</summary>
internal static class WebFailureText
{
    /// <summary>The sentence for a call that never reached the API.</summary>
    internal const string Unreachable = "The API could not be reached.";

    /// <summary>Whether an exception is an expected failure of a call to the API, as opposed to a programming error.</summary>
    /// <param name="fault">The exception.</param>
    /// <returns>True for an API problem, a transport failure or a malformed answer.</returns>
    internal static bool IsExpected(Exception fault) => fault is ApiClientException or HttpRequestException or IOException;

    /// <summary>Describes a failure for the user.</summary>
    /// <param name="fault">The exception.</param>
    /// <returns>A sentence ending in a full stop.</returns>
    internal static string Describe(Exception fault)
    {
        ArgumentNullException.ThrowIfNull(fault);
        if (fault is ApiProblemException problem)
        {
            var title = problem.Title.TrimEnd('.');
            return problem.Detail is { Length: > 0 } detail ? $"{title}. {detail.TrimEnd('.')}." : $"{title}.";
        }

        return fault is HttpRequestException or IOException ? Unreachable : "The API answered in a form the web head does not understand.";
    }
}
