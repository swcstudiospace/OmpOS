namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Typed backend failure. The message is a fixed category sentence and never includes a title or typed text.</summary>
public sealed class ComputerBackendException : Exception
{
    /// <summary>Creates an exception for <see cref="ComputerFailure.Unavailable"/>.</summary>
    public ComputerBackendException()
        : this(ComputerFailure.Unavailable)
    {
    }

    /// <summary>Creates an exception. <paramref name="message"/> must not include a window title or typed text.</summary>
    /// <param name="message">The failure sentence.</param>
    public ComputerBackendException(string message)
        : base(message)
    {
        Failure = ComputerFailure.Unavailable;
    }

    /// <summary>Creates an exception with an inner exception. <paramref name="message"/> must not include a window title or typed text.</summary>
    /// <param name="message">The failure sentence.</param>
    /// <param name="innerException">The cause.</param>
    public ComputerBackendException(string message, Exception innerException)
        : base(message, innerException)
    {
        Failure = ComputerFailure.Unavailable;
    }

    /// <summary>Creates an exception whose message is the fixed sentence for <paramref name="failure"/>.</summary>
    /// <param name="failure">The failure category.</param>
    public ComputerBackendException(ComputerFailure failure)
        : base(Sentence(failure))
    {
        Failure = failure;
    }

    /// <summary>The failure category.</summary>
    public ComputerFailure Failure { get; }

    /// <summary>Fixed sentence for <paramref name="failure"/>. No title and no typed text.</summary>
    /// <param name="failure">The failure category.</param>
    /// <returns>The sentence.</returns>
    public static string Sentence(ComputerFailure failure) => failure switch
    {
        ComputerFailure.PermissionMissing => "Computer Use permission is missing.",
        ComputerFailure.Unavailable => "Computer Use backend is unavailable.",
        ComputerFailure.TargetGone => "Computer Use target is gone.",
        ComputerFailure.Elevated => "Computer Use target is elevated.",
        ComputerFailure.SecureDesktop => "Computer Use secure desktop is active.",
        ComputerFailure.Timeout => "Computer Use backend timed out.",
        _ => throw new ArgumentOutOfRangeException(nameof(failure)),
    };
}

