namespace OmpOS.Api.IntegrationTests;

/// <summary>Flattens an exception chain so a startup-failure assertion can search every message in it.</summary>
internal static class StartupFailureText
{
    /// <summary>Joins the message of an exception and of each inner exception, outermost first.</summary>
    /// <param name="exception">The exception to flatten.</param>
    /// <returns>The messages joined with <c> | </c>.</returns>
    public static string Of(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var parts = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            parts.Add(current.Message);
        }

        return string.Join(" | ", parts);
    }
}
