using System.Security.Authentication;

namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>Classifies transport exceptions for the managed install.</summary>
internal static class OmpFailureMapper
{
    /// <summary>
    /// Reports whether the exception, or one it wraps, is a certificate or TLS trust failure. Such a failure is shown as a
    /// trust problem and never retried with validation disabled.
    /// </summary>
    /// <param name="exception">The exception to inspect.</param>
    /// <returns>True when an <see cref="AuthenticationException"/> is in the chain.</returns>
    public static bool IsTrustFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is AuthenticationException)
            {
                return true;
            }
        }

        return false;
    }
}
